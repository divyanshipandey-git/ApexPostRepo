using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApexPost.Api.DTOs;

/// <summary>
/// Request to create a new shipment.
/// </summary>
public sealed class CreateShipmentRequest
{
    /// <summary>
    /// Unique tracking number in format APX-YYYYMMDD (a real calendar date).
    /// </summary>
    [Required]
    [RegularExpression(TrackingNumberFormat.Pattern, ErrorMessage = TrackingNumberFormat.ErrorMessage)]
    [JsonPropertyName("trackingNumber")]
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>
    /// Package weight in kilograms (0.1 - 70 kg).
    /// </summary>
    [Required]
    [Range(typeof(decimal), ShipmentLimits.MinWeightKg, ShipmentLimits.MaxWeightKg, ErrorMessage = ShipmentLimits.WeightErrorMessage)]
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
    [RegularExpression(PhoneFormat.E164Pattern, ErrorMessage = PhoneFormat.ErrorMessage)]
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
    [RegularExpression("^[A-Z]{2}$", ErrorMessage = "Country must be a 2-letter upper-case ISO 3166-1 alpha-2 code")]
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

/// <summary>
/// Single definition of the tracking-number format used by every request DTO.
/// </summary>
public static class TrackingNumberFormat
{
    /// <summary>APX- followed by a calendar date: YYYY (1900-2099), MM (01-12), DD (01-31).</summary>
    public const string Pattern = @"^APX-(19|20)\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])$";

    public const string ErrorMessage = "Tracking number must match format APX-YYYYMMDD (for example APX-20241107)";
}

/// <summary>
/// Single definition of the phone-number format used by every request DTO.
/// </summary>
public static class PhoneFormat
{
    public const string E164Pattern = @"^\+?[1-9]\d{1,14}$";

    public const string ErrorMessage = "Phone number must be in E.164 format (for example +15551234567)";
}

/// <summary>
/// Single definition of the shipment weight limits used by every request DTO.
/// </summary>
public static class ShipmentLimits
{
    public const string MinWeightKg = "0.1";
    public const string MaxWeightKg = "70";
    public const string WeightErrorMessage = "Weight must be between 0.1 and 70 kg";
}
