using System.Diagnostics;
using System.Security.Claims;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Services.Auditing;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ApexPost.Web.Filters;

public sealed class AuditActionFilter(IAuditStore auditStore) : IAsyncActionFilter
{
    private static readonly HashSet<string> ExcludedRouteKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "token", "apikey", "apiKey", "identity", "document", "card", "email"
    };

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var timer = Stopwatch.StartNew();
        ActionExecutedContext? executed = null;
        try
        {
            executed = await next();
        }
        catch
        {
            timer.Stop();
            await WriteRecordAsync(context, StatusCodes.Status500InternalServerError, timer.ElapsedMilliseconds);
            throw;
        }

        timer.Stop();
        await WriteRecordAsync(
            context,
            GetStatusCode(context, executed),
            timer.ElapsedMilliseconds);
    }

    private Task WriteRecordAsync(
        ActionExecutingContext context,
        int statusCode,
        long durationMilliseconds)
    {
        var user = context.HttpContext.User;
        var routeValues = context.RouteData.Values
            .Where(pair => !ExcludedRouteKeys.Contains(pair.Key))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.ToString() is { Length: <= 256 } value ? value : null,
                StringComparer.OrdinalIgnoreCase);

        var record = new AuditActionRecord(
            DateTimeOffset.UtcNow,
            user.FindFirstValue("StaffId"),
            user.FindFirstValue("Role") ?? user.FindFirstValue(ClaimTypes.Role),
            user.FindFirstValue("BranchCode"),
            context.RouteData.Values["controller"]?.ToString() ?? "Unknown",
            context.RouteData.Values["action"]?.ToString() ?? "Unknown",
            routeValues,
            statusCode,
            durationMilliseconds,
            CorrelationIdMiddleware.GetCorrelationId(context.HttpContext));

        return auditStore.WriteAsync(record, context.HttpContext.RequestAborted);
    }

    private static int GetStatusCode(ActionExecutingContext context, ActionExecutedContext? executed)
    {
        if (executed?.Exception is not null && !executed.ExceptionHandled)
        {
            return StatusCodes.Status500InternalServerError;
        }

        if (executed?.Result is IStatusCodeActionResult statusResult && statusResult.StatusCode is int statusCode)
        {
            return statusCode;
        }

        if (executed?.Result is Microsoft.AspNetCore.Mvc.RedirectResult or
            Microsoft.AspNetCore.Mvc.RedirectToActionResult or
            Microsoft.AspNetCore.Mvc.RedirectToRouteResult)
        {
            return StatusCodes.Status302Found;
        }

        return context.HttpContext.Response.StatusCode;
    }
}
