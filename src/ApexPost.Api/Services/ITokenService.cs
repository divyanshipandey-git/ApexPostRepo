using System.Security.Claims;

namespace ApexPost.Api.Services;

/// <summary>
/// Service for issuing JWT tokens (development only).
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Issues a development JWT token for a courier.
    /// </summary>
    /// <param name="courierId">The courier ID to include in the token.</param>
    /// <param name="role">The role (Courier or Admin).</param>
    /// <returns>The JWT token string.</returns>
    string IssueToken(string courierId, string role = "Courier");
}