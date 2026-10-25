using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Middleware;

public sealed class EndpointProtectionMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var apiKeyProtection = endpoint?.Metadata.GetMetadata<ApiKeyProtectedAttribute>();
        if (apiKeyProtection is not null)
        {
            var expected = configuration["Integration:ApiKey"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Integration endpoint configuration is unavailable.");
                return;
            }

            if (!HasValidApiKey(context, expected))
            {
                await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "A valid integration API key is required.");
                return;
            }
        }

        var ipProtection = endpoint?.Metadata.GetMetadata<IpAllowlistProtectedAttribute>();
        if (ipProtection is not null && !IsAllowedIp(context, ipProtection.ConfigurationKey))
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "This client is not permitted to access the endpoint.");
            return;
        }

        await next(context);
    }

    private static bool HasValidApiKey(HttpContext context, string expected)
    {
        var supplied = context.Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var valid = CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
        CryptographicOperations.ZeroMemory(expectedHash);
        CryptographicOperations.ZeroMemory(suppliedHash);
        return valid;
    }

    private bool IsAllowedIp(HttpContext context, string configurationKey)
    {
        var configured = configuration.GetSection(configurationKey).Get<string[]>() ?? [];
        var remoteIp = context.Connection.RemoteIpAddress;
        return remoteIp is not null && configured
            .Select(value => IPAddress.TryParse(value, out var ip) ? ip : null)
            .Where(static ip => ip is not null)
            .Any(ip => Normalize(ip!).Equals(Normalize(remoteIp)));
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new ProblemDetails { Type = "about:blank", Title = title, Status = statusCode },
            cancellationToken: context.RequestAborted);
    }
}
