using System.Diagnostics;

namespace ApexPost.Web.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "ApexPost.CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetValidId(context.Request.Headers[HeaderName].ToString())
            ?? ActivityTraceId.CreateRandom().ToString();

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    public static string? GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;

    private static string? GetValidId(string value)
    {
        if (value.Length is 0 or > 128)
        {
            return null;
        }

        return value.All(static character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
            ? value
            : null;
    }
}
