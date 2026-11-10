using ApexPost.Api.DTOs;
using ApexPost.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Extensions;

/// <summary>
/// Development-only helper that issues test JWTs. It is deliberately restrictive so it cannot be abused
/// to mint a token for an arbitrary courier:
/// <list type="bullet">
/// <item>it is only mapped when the environment is Development;</item>
/// <item>it only answers requests that come from the local machine (loopback);</item>
/// <item>the courier id must be listed in <c>DevTokens:CourierIds</c>;</item>
/// <item>the Admin role is only issued for ids listed in <c>DevTokens:AdminIds</c>.</item>
/// </list>
/// </summary>
public static class DevelopmentTokenEndpoint
{
    public static IEndpointRouteBuilder MapDevelopmentTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/dev/token", (
            TokenRequest request,
            HttpContext httpContext,
            IConfiguration configuration,
            ITokenService tokenService) =>
        {
            var remote = httpContext.Connection.RemoteIpAddress;
            if (remote is null || !System.Net.IPAddress.IsLoopback(remote))
            {
                return Results.Problem(
                    title: "Forbidden",
                    detail: "The development token endpoint only answers requests from the local machine.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (string.IsNullOrWhiteSpace(request.CourierId))
            {
                return Results.Problem(title: "Bad Request", detail: "CourierId is required", statusCode: StatusCodes.Status400BadRequest);
            }

            var role = string.IsNullOrWhiteSpace(request.Role) ? "Courier" : request.Role;
            if (role is not ("Courier" or "Admin"))
            {
                return Results.Problem(title: "Bad Request", detail: "Role must be 'Courier' or 'Admin'", statusCode: StatusCodes.Status400BadRequest);
            }

            var allowedCouriers = configuration.GetSection("DevTokens:CourierIds").Get<string[]>() ?? [];
            var allowedAdmins = configuration.GetSection("DevTokens:AdminIds").Get<string[]>() ?? [];
            var allowed = role == "Admin" ? allowedAdmins : allowedCouriers;

            if (!allowed.Contains(request.CourierId, StringComparer.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    title: "Forbidden",
                    detail: $"'{request.CourierId}' is not a configured development {role} id (DevTokens section).",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var token = tokenService.IssueToken(request.CourierId, role);
            return Results.Ok(new TokenResponse { AccessToken = token, ExpiresIn = 28800 });
        })
        .WithName("GetDevToken")
        .WithSummary("Get a development JWT token")
        .WithDescription("Development only, local requests only. Issues a token for a configured test courier or admin id.")
        .Produces<TokenResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .AllowAnonymous()
        .ExcludeFromDescription();

        return app;
    }
}
