using System;
using System.Text.Json.Serialization;

namespace ApexPost.Courier.Console.Models;

/// <summary>
/// Token storage model for persisting JWT tokens locally.
/// </summary>
public sealed class TokenData
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("courier_id")]
    public string CourierId { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = "Courier";

    [JsonPropertyName("issued_at")]
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}