using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ApexPost.Api.Services;

/// <summary>
/// Development-only token service that issues JWT tokens for testing.
/// </summary>
public sealed class DevelopmentTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public DevelopmentTokenService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public string IssueToken(string courierId, string role = "Courier")
    {
        var issuer = _configuration["Jwt:Issuer"] ?? "https://apexpost.example.com";
        var audience = _configuration["Jwt:Audience"] ?? "apexpost-api";
        var signingKey = _configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey must be configured in User Secrets or appsettings.json");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, courierId),
            new(ClaimTypes.Role, role),
            new("courierId", courierId),
            new(JwtRegisteredClaimNames.Sub, courierId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}