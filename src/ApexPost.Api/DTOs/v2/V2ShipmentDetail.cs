using System.Text.Json.Serialization;
// ShipmentStatus, ScanType and GpsCoordinates are shared with v1 (ApexPost.Api.DTOs): one definition, one schema.
using ApexPost.Api.DTOs;

namespace ApexPost.Api.DTOs.v2;

/// <summary>
/// v2 Shipment detail response with nested recipient and latest scan summary.
/// </summary>
public sealed class V2ShipmentDetail
{
    /// <summary>
    /// Unique tracking number in format APX-NNNNNNNN.
    /// </summary>
    [JsonPropertyName("trackingNumber")]
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>
    /// Current status of the shipment as string enum.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ShipmentStatus Status { get; init; }

    /// <summary>
    /// Package weight in kilograms.
    /// </summary>
    [JsonPropertyName("weightKg")]
    public decimal WeightKg { get; init; }

    /// <summary>
    /// Nested recipient information object.
    /// </summary>
    [JsonPropertyName("recipient")]
    public V2RecipientInfo Recipient { get; init; } = new();

    /// <summary>
    /// Optional special handling instructions.
    /// </summary>
    [JsonPropertyName("specialInstructions")]
    public string? SpecialInstructions { get; init; }

    /// <summary>
    /// Optional declared value for insurance.
    /// </summary>
    [JsonPropertyName("declaredValue")]
    public decimal? DeclaredValue { get; init; }

    /// <summary>
    /// UTC timestamp when the shipment was created.
    /// </summary>
    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// UTC timestamp when the shipment was last updated.
    /// </summary>
    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset? UpdatedAtUtc { get; init; }

    /// <summary>
    /// Summary of the most recent scan.
    /// </summary>
    [JsonPropertyName("latestScan")]
    public V2LatestScanSummary? LatestScan { get; init; }

    /// <summary>
    /// Complete scan history.
    /// </summary>
    [JsonPropertyName("scans")]
    public IReadOnlyList<V2ScanSummary> Scans { get; init; } = [];
}

/// <summary>
/// Nested recipient information for v2 responses.
/// </summary>
public sealed class V2RecipientInfo
{
    /// <summary>
    /// Recipient full name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Recipient phone number in E.164 format.
    /// </summary>
    [JsonPropertyName("phone")]
    public string Phone { get; init; } = string.Empty;

    /// <summary>
    /// Recipient street address.
    /// </summary>
    [JsonPropertyName("address")]
    public string Address { get; init; } = string.Empty;

    /// <summary>
    /// Recipient city.
    /// </summary>
    [JsonPropertyName("city")]
    public string City { get; init; } = string.Empty;

    /// <summary>
    /// Recipient postal code.
    /// </summary>
    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; init; }

    /// <summary>
    /// Recipient country code (ISO 3166-1 alpha-2).
    /// </summary>
    [JsonPropertyName("country")]
    public string Country { get; init; } = "US";
}

/// <summary>
/// Summary of the most recent scan for v2 responses.
/// </summary>
public sealed class V2LatestScanSummary
{
    /// <summary>
    /// Type of the latest scan.
    /// </summary>
    [JsonPropertyName("scanType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ScanType ScanType { get; init; }

    /// <summary>
    /// Location where the latest scan was performed.
    /// </summary>
    [JsonPropertyName("location")]
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the latest scan was performed.
    /// </summary>
    [JsonPropertyName("scannedAtUtc")]
    public DateTimeOffset ScannedAtUtc { get; init; }
}

/// <summary>
/// Scan summary for v2 responses with status as string enum.
/// </summary>
public sealed class V2ScanSummary
{
    /// <summary>
    /// Unique scan ID from Part A offline store (format: scan::{Guid}).
    /// </summary>
    [JsonPropertyName("scanId")]
    public string ScanId { get; init; } = string.Empty;

    /// <summary>
    /// Type of scan performed as string enum.
    /// </summary>
    [JsonPropertyName("scanType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ScanType ScanType { get; init; }

    /// <summary>
    /// Courier ID who performed the scan.
    /// </summary>
    [JsonPropertyName("courierId")]
    public string CourierId { get; init; } = string.Empty;

    /// <summary>
    /// Location where scan was performed.
    /// </summary>
    [JsonPropertyName("location")]
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// UTC timestamp when scan was performed.
    /// </summary>
    [JsonPropertyName("scannedAtUtc")]
    public DateTimeOffset ScannedAtUtc { get; init; }

    /// <summary>
    /// Optional notes about the scan.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    /// <summary>
    /// Optional GPS coordinates of the scan location.
    /// </summary>
    [JsonPropertyName("gpsCoordinates")]
    public GpsCoordinates? GpsCoordinates { get; init; }

    /// <summary>
    /// Optional recipient signature name (for Delivered scans).
    /// </summary>
    [JsonPropertyName("recipientSignature")]
    public string? RecipientSignature { get; init; }
}
