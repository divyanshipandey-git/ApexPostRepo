using System.Diagnostics;
using System.Text.Json;
using ApexPost.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Middleware;

public sealed class SafeExceptionMiddleware(
    RequestDelegate next,
    ILogger<SafeExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Request was cancelled by the client. TraceId {TraceId}",
                Activity.Current?.Id ?? context.TraceIdentifier);
            throw;
        }
        catch (ConcurrencyConflictException exception) when (!context.Response.HasStarted)
        {
            await WriteConcurrencyProblemAsync(context, exception);
        }
        catch (UniqueKeyConflictException exception) when (!context.Response.HasStarted)
        {
            await WriteProblemAsync(
                context,
                exception,
                StatusCodes.Status409Conflict,
                "A record with the same unique key already exists.");
        }
        catch (DbUpdateConcurrencyException exception) when (!context.Response.HasStarted)
        {
            await WriteConcurrencyProblemAsync(context, exception);
        }
        catch (ValidationException exception) when (!context.Response.HasStarted)
        {
            await WriteProblemAsync(
                context,
                exception,
                StatusCodes.Status400BadRequest,
                "The request contains invalid values.");
        }
        catch (KeyNotFoundException exception) when (!context.Response.HasStarted)
        {
            await WriteProblemAsync(
                context,
                exception,
                StatusCodes.Status404NotFound,
                "The requested resource was not found.");
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
            logger.LogError(exception, "Unhandled request failure. TraceId {TraceId}", traceId);

            var problem = CreateProblem(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.");
            if (correlationId is not null)
            {
                problem.Extensions["correlationId"] = correlationId;
            }

            await WriteProblemAsync(context, problem);
        }
    }

    private async Task WriteConcurrencyProblemAsync(HttpContext context, Exception exception)
    {
        await WriteProblemAsync(
            context,
            exception,
            StatusCodes.Status409Conflict,
            "The record changed while this operation was in progress. Refresh and retry.");
    }

    private async Task WriteProblemAsync(
        HttpContext context,
        Exception exception,
        int statusCode,
        string title)
    {
        var logLevel = statusCode >= StatusCodes.Status500InternalServerError
            ? LogLevel.Error
            : LogLevel.Warning;
        logger.Log(logLevel, exception, "Request failed with status {StatusCode}. TraceId {TraceId}",
            statusCode, Activity.Current?.Id ?? context.TraceIdentifier);
        await WriteProblemAsync(context, CreateProblem(context, statusCode, title));
    }

    private static async Task WriteProblemAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            problem,
            cancellationToken: context.RequestAborted);
    }

    private static ProblemDetails CreateProblem(HttpContext context, int statusCode, string title)
    {
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Status = statusCode
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        return problem;
    }
}
