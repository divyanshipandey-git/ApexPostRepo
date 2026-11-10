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

        // Keys come from configuration only (User Secrets / environment): "PartnerApiKey" for a single key, or
        // "PartnerApiKeys:<PartnerName>" for one key per partner. Nothing is hard-coded or committed.
        var expectedKeys = configuration.GetSection("PartnerApiKeys").GetChildren()
            .Select(c => c.Value)
            .Append(configuration["PartnerApiKey"])
            .Append(configuration["ApiKeys:Partner"])
            .Append(Environment.GetEnvironmentVariable("APEXPOST_PARTNER_API_KEY"))
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => Encoding.UTF8.GetBytes(k!))
            .ToList();

        if (expectedKeys.Count == 0)
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
        var matched = false;
        foreach (var expected in expectedKeys)
        {
            matched |= CryptographicOperations.FixedTimeEquals(provided, expected);
        }

        if (!matched)
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
