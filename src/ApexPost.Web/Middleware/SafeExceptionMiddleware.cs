using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

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
}
