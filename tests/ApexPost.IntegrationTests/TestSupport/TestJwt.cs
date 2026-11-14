using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ApexPost.IntegrationTests.TestSupport;

/// <summary>
/// Mints real signed JWTs with the fake test key. The API validates them exactly like production tokens
/// (issuer, audience, lifetime, signature), so the test authentication setup exercises the real JwtBearer pipeline.
/// </summary>
public static class TestJwt
{
    public const string Issuer = "https://apexpost.test";
    public const string Audience = "apexpost-api-test";

    public static string Courier(string courierId = "COURIER-001") => Create(courierId, "Courier");

    public static string Admin(string adminId = "ADMIN-001") => Create(adminId, "Admin");

    public static string Create(
        string subject,
        string role,
        DateTime? expires = null,
        string signingKey = ApexApiFactory.TestSigningKey)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Role, role),
            new("courierId", subject),
            new(JwtRegisteredClaimNames.Sub, subject)
        };

        var now = DateTime.UtcNow;
        var expiry = expires ?? now.AddHours(1);
        var token = new JwtSecurityToken(Issuer, Audience, claims, expiry.AddHours(-2), expiry, credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
