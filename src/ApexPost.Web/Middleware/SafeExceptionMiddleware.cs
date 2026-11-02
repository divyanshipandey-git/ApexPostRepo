using System.Diagnostics;
using System.Text.Json;
using ApexPost.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        catch (DbUpdateConcurrencyException exception) when (!context.Response.HasStarted)
        {
            await WriteConcurrencyProblemAsync(context, exception);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
            logger.LogError(exception, "Unhandled request failure. TraceId {TraceId}", traceId);

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Type = "about:blank",
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError
            };
            problem.Extensions["traceId"] = traceId;
            if (correlationId is not null)
            {
                problem.Extensions["correlationId"] = correlationId;
            }

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                problem,
                cancellationToken: context.RequestAborted);
        }
    }

    private async Task WriteConcurrencyProblemAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        logger.LogWarning(exception, "A concurrent database update was rejected. TraceId {TraceId}", traceId);
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = "The record changed while this operation was in progress. Refresh and retry.",
            Status = StatusCodes.Status409Conflict
        };
        problem.Extensions["traceId"] = traceId;
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            problem,
            cancellationToken: context.RequestAborted);
    }
}
