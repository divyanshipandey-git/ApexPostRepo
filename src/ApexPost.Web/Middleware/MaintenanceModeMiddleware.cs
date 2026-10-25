using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Middleware;

public sealed class MaintenanceModeMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var isHealthCheck = context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase);
        if (!configuration.GetValue<bool>("Maintenance:Enabled") || isHealthCheck)
        {
            await next(context);
            return;
        }

        var retryAfter = Math.Clamp(configuration.GetValue("Maintenance:RetryAfterSeconds", 3600), 1, 86400);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Type = "about:blank",
            Title = "The service is temporarily unavailable for maintenance.",
            Status = StatusCodes.Status503ServiceUnavailable
        }, cancellationToken: context.RequestAborted);
    }
}
