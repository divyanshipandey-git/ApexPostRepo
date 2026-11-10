using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApexPost.Api.DTOs;

/// <summary>
/// Request to bulk upload offline scans from courier devices.
/// </summary>
public sealed class ScanUploadRequest
{
    /// <summary>
    /// List of scan documents to upload.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one scan is required")]
    [MaxLength(100, ErrorMessage = "Maximum 100 scans per upload")]
    [JsonPropertyName("scans")]
    public List<ScanDocumentRequest> Scans { get; init; } = [];
}

/// <summary>
/// Individual scan document from courier offline store.
/// </summary>
public sealed class ScanDocumentRequest
{
    /// <summary>
    /// Unique scan ID from Part A offline store (format: scan::{Guid}).
    /// </summary>
    [Required]
    [RegularExpression(@"^scan::[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        ErrorMessage = "Scan ID must be in format scan::{Guid}")]
    [JsonPropertyName("scanId")]
    public string ScanId { get; init; } = string.Empty;

    /// <summary>
    /// Type of scan performed.
    /// </summary>
    [Required]
    [JsonPropertyName("scanType")]
    [EnumDataType(typeof(ScanType), ErrorMessage = "Invalid scan type")]
    public ScanType ScanType { get; init; }

    /// <summary>
    /// Courier ID who performed the scan.
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    [JsonPropertyName("courierId")]
    public string CourierId { get; init; } = string.Empty;

    /// <summary>
    /// Location where scan was performed.
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    [JsonPropertyName("location")]
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// UTC timestamp when scan was performed. Business rules that depend on other data (not in the future,
    /// not before the shipment was created, valid scan sequence) are applied per scan by the server and
    /// reported in <c>rejectionDetails</c> instead of failing the whole batch.
    /// </summary>
    [Required]
    [JsonPropertyName("scannedAtUtc")]
    public DateTimeOffset ScannedAtUtc { get; init; }

    /// <summary>
    /// Notes about the scan (a Failed scan without a reason in the notes is rejected with a reason).
    /// </summary>
    [StringLength(500)]
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    /// <summary>
    /// Optional GPS coordinates of the scan location.
    /// </summary>
    [JsonPropertyName("gpsCoordinates")]
    public GpsCoordinates? GpsCoordinates { get; init; }

    /// <summary>
    /// Recipient signature name (a Delivered scan without one is rejected with a reason).
    /// </summary>
    [StringLength(100)]
    [JsonPropertyName("recipientSignature")]
    public string? RecipientSignature { get; init; }
}

/// <summary>
/// Scan type enumeration matching Part A.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScanType
{
    Pickup = 0,
    InTransit = 1,
    Delivered = 2,
    Failed = 3
}