using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.Security.Cryptography;

namespace ApexPost.Api.Extensions;

/// <summary>
/// Endpoint filter that validates the X-Api-Key header for partner endpoints.
/// </summary>
public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    private readonly IConfiguration _configuration;

    public ApiKeyEndpointFilter(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var apiKey = _configuration["PartnerApiKey"] 
            ?? _configuration["ApiKeys:Partner"] 
            ?? Environment.GetEnvironmentVariable("APEXPOST_PARTNER_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Results.Problem(
                detail: "Partner API key is not configured on the server",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Server Configuration Error",
                type: "https://tools.ietf.org/html/rfc7231#section-6.6.1");
        }

        if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out StringValues providedKey))
        {
            return Results.Problem(
                detail: "Missing X-Api-Key header",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.1");
        }

        // Constant-time comparison to prevent timing attacks
        var provided = providedKey.ToString();
        if (!CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(provided),
            System.Text.Encoding.UTF8.GetBytes(apiKey)))
        {
            return Results.Problem(
                detail: "Invalid API key",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.1");
        }

        return await next(context);
    }
}