using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApexPost.Api.DTOs;

/// <summary>
/// Request to create a new shipment.
/// </summary>
public sealed class CreateShipmentRequest
{
    /// <summary>
    /// Unique tracking number in format APX-YYYYMMDD.
    /// </summary>
    [Required]
    [RegularExpression(@"^APX-\d{8}$", ErrorMessage = "Tracking number must match format APX-YYYYMMDD")]
    [JsonPropertyName("trackingNumber")]
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>
    /// Package weight in kilograms (0.1 - 70 kg).
    /// </summary>
    [Required]
    [Range(0.1, 70.0, ErrorMessage = "Weight must be between 0.1 and 70 kg")]
    [JsonPropertyName("weightKg")]
    public decimal WeightKg { get; init; }

    /// <summary>
    /// Recipient full name.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Recipient name is required")]
    [JsonPropertyName("recipientName")]
    public string RecipientName { get; init; } = string.Empty;

    /// <summary>
    /// Recipient phone number in E.164 format.
    /// </summary>
    [Required]
    [Phone(ErrorMessage = "Phone number must be in valid format (E.164 recommended)")]
    [JsonPropertyName("recipientPhone")]
    public string RecipientPhone { get; init; } = string.Empty;

    /// <summary>
    /// Recipient street address.
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Recipient address is required")]
    [JsonPropertyName("recipientAddress")]
    public string RecipientAddress { get; init; } = string.Empty;

    /// <summary>
    /// Recipient city.
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Recipient city is required")]
    [JsonPropertyName("recipientCity")]
    public string RecipientCity { get; init; } = string.Empty;

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
    public string? RecipientCountry { get; init; } = "US";

    /// <summary>
    /// Optional special handling instructions.
    /// </summary>
    [StringLength(500)]
    [JsonPropertyName("specialInstructions")]
    public string? SpecialInstructions { get; init; }

    /// <summary>
    /// Optional declared value for insurance.
    /// </summary>
    [Range(0, 10000, ErrorMessage = "Declared value must be between 0 and 10000")]
    [JsonPropertyName("declaredValue")]
    public decimal? DeclaredValue { get; init; }
}