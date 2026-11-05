using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApexPost.Api.DTOs;

/// <summary>
/// Request to update an existing shipment's editable details.
/// </summary>
public sealed class UpdateShipmentRequest
{
    /// <summary>
    /// Package weight in kilograms (0.1 - 70 kg).
    /// </summary>
    [Range(0.1, 70.0, ErrorMessage = "Weight must be between 0.1 and 70 kg")]
    [JsonPropertyName("weightKg")]
    public decimal? WeightKg { get; init; }

    /// <summary>
    /// Recipient full name.
    /// </summary>
    [StringLength(100, MinimumLength = 1)]
    [JsonPropertyName("recipientName")]
    public string? RecipientName { get; init; }

    /// <summary>
    /// Recipient phone number in E.164 format.
    /// </summary>
    [Phone]
    [JsonPropertyName("recipientPhone")]
    public string? RecipientPhone { get; init; }

    /// <summary>
    /// Recipient street address.
    /// </summary>
    [StringLength(200, MinimumLength = 1)]
    [JsonPropertyName("recipientAddress")]
    public string? RecipientAddress { get; init; }

    /// <summary>
    /// Recipient city.
    /// </summary>
    [StringLength(50, MinimumLength = 1)]
    [JsonPropertyName("recipientCity")]
    public string? RecipientCity { get; init; }

    /// <summary>
    /// Recipient postal code.
    /// </summary>
    [StringLength(20)]
    [JsonPropertyName("recipientPostalCode")]
    public string? RecipientPostalCode { get; init; }

    /// <summary>
    /// Recipient country code (ISO 3166-1 alpha-2).
    /// </summary>
    [StringLength(2, MinimumLength = 2)]
    [JsonPropertyName("recipientCountry")]
    public string? RecipientCountry { get; init; }

    /// <summary>
    /// Optional special handling instructions.
    /// </summary>
    [StringLength(500)]
    [JsonPropertyName("specialInstructions")]
    public string? SpecialInstructions { get; init; }

    /// <summary>
    /// Optional declared value for insurance.
    /// </summary>
    [Range(0, 10000)]
    [JsonPropertyName("declaredValue")]
    public decimal? DeclaredValue { get; init; }
}