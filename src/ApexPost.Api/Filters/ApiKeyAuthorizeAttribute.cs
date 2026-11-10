using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Api.Filters;

/// <summary>
/// Requires a valid <c>X-Api-Key</c> header. Used by the partner endpoints, which are not JWT protected.
/// Failures are returned as RFC 9457 problem details. The key is compared in constant time.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApiKeyAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string HeaderName = "X-Api-Key";

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expectedKey = configuration["PartnerApiKey"]
            ?? configuration["ApiKeys:Partner"]
            ?? Environment.GetEnvironmentVariable("APEXPOST_PARTNER_API_KEY");

        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            context.Result = Problem(
                StatusCodes.Status500InternalServerError,
                "Server Configuration Error",
                "Partner API key is not configured on the server");
            return Task.CompletedTask;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var providedKey)
            || string.IsNullOrEmpty(providedKey.ToString()))
        {
            context.Result = Problem(StatusCodes.Status401Unauthorized, "Unauthorized", $"Missing {HeaderName} header");
            return Task.CompletedTask;
        }

        var provided = Encoding.UTF8.GetBytes(providedKey.ToString());
        var expected = Encoding.UTF8.GetBytes(expectedKey);

        if (!CryptographicOperations.FixedTimeEquals(provided, expected))
        {
            context.Result = Problem(StatusCodes.Status401Unauthorized, "Unauthorized", "Invalid API key");
        }

        return Task.CompletedTask;
    }

    private static ObjectResult Problem(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
}
