using System.ComponentModel.DataAnnotations;

namespace ApexPost.Api.DTOs;

/// <summary>
/// Request to obtain a development JWT token.
/// </summary>
public sealed class TokenRequest
{
    /// <summary>
    /// The courier ID to include in the token.
    /// </summary>
    [Required]
    public string CourierId { get; init; } = string.Empty;

    /// <summary>
    /// The role for the token (Courier or Admin).
    /// </summary>
    public string? Role { get; init; }
}

/// <summary>
/// Response containing the JWT token.
/// </summary>
public sealed class TokenResponse
{
    /// <summary>
    /// The JWT access token.
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// The token type (always "Bearer").
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// Token lifetime in seconds.
    /// </summary>
    public int ExpiresIn { get; init; }
}