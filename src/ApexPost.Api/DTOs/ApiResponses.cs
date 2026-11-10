using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.DTOs;

/// <summary>
/// Response for scan upload with counts.
/// </summary>
public sealed class ScanUploadResponse
{
    /// <summary>
    /// Number of scans successfully accepted and stored.
    /// </summary>
    [JsonPropertyName("accepted")]
    public int Accepted { get; set; }

    /// <summary>
    /// Number of scans skipped because they already existed (idempotent).
    /// </summary>
    [JsonPropertyName("duplicatesSkipped")]
    public int DuplicatesSkipped { get; set; }

    /// <summary>
    /// The scan IDs that were skipped as duplicates. Duplicates are not errors, so they are
    /// listed here and never in <see cref="RejectionDetails"/>.
    /// </summary>
    [JsonPropertyName("duplicateScanIds")]
    public List<string> DuplicateScanIds { get; set; } = [];

    /// <summary>
    /// Number of scans rejected with reasons.
    /// </summary>
    [JsonPropertyName("rejected")]
    public int Rejected { get; set; }

    /// <summary>
    /// Details for rejected scans (genuine rejections only, never duplicates).
    /// </summary>
    [JsonPropertyName("rejectionDetails")]
    public List<ScanRejectionDetail> RejectionDetails { get; set; } = [];

    /// <summary>
    /// Total scans in the request.
    /// </summary>
    [JsonPropertyName("totalProcessed")]
    public int TotalProcessed => Accepted + DuplicatesSkipped + Rejected;
}

/// <summary>
/// Details for a rejected scan.
/// </summary>
public sealed class ScanRejectionDetail
{
    /// <summary>
    /// The scan ID that was rejected.
    /// </summary>
    [JsonPropertyName("scanId")]
    public string ScanId { get; init; } = string.Empty;

    /// <summary>
    /// The reason for rejection.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Paged response wrapper.
/// </summary>
public sealed class PagedResponse<T>
{
    [JsonPropertyName("items")]
    public IReadOnlyList<T> Items { get; init; } = [];

    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }

    [JsonPropertyName("totalPages")]
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

/// <summary>
/// Shipment summary for list responses.
/// </summary>
public sealed class ShipmentSummary
{
    [JsonPropertyName("trackingNumber")]
    public string TrackingNumber { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public ShipmentStatus Status { get; init; }

    [JsonPropertyName("recipientName")]
    public string RecipientName { get; init; } = string.Empty;

    [JsonPropertyName("recipientCity")]
    public string RecipientCity { get; init; } = string.Empty;

    [JsonPropertyName("weightKg")]
    public decimal WeightKg { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    [JsonPropertyName("lastScanAtUtc")]
    public DateTimeOffset? LastScanAtUtc { get; init; }

    [JsonPropertyName("scanCount")]
    public int ScanCount { get; init; }
}

/// <summary>
/// Detailed shipment response.
/// </summary>
public sealed class ShipmentDetail
{
    [JsonPropertyName("trackingNumber")]
    public string TrackingNumber { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public ShipmentStatus Status { get; init; }

    [JsonPropertyName("weightKg")]
    public decimal WeightKg { get; init; }

    [JsonPropertyName("recipientName")]
    public string RecipientName { get; init; } = string.Empty;

    [JsonPropertyName("recipientPhone")]
    public string RecipientPhone { get; init; } = string.Empty;

    [JsonPropertyName("recipientAddress")]
    public string RecipientAddress { get; init; } = string.Empty;

    [JsonPropertyName("recipientCity")]
    public string RecipientCity { get; init; } = string.Empty;

    [JsonPropertyName("recipientPostalCode")]
    public string? RecipientPostalCode { get; init; }

    [JsonPropertyName("recipientCountry")]
    public string? RecipientCountry { get; init; }

    [JsonPropertyName("specialInstructions")]
    public string? SpecialInstructions { get; init; }

    [JsonPropertyName("declaredValue")]
    public decimal? DeclaredValue { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset? UpdatedAtUtc { get; init; }

    [JsonPropertyName("scans")]
    public IReadOnlyList<ScanSummary> Scans { get; init; } = [];
}

/// <summary>
/// Scan summary for shipment detail.
/// </summary>
public sealed class ScanSummary
{
    [JsonPropertyName("scanId")]
    public string ScanId { get; init; } = string.Empty;

    [JsonPropertyName("scanType")]
    public ScanType ScanType { get; init; }

    [JsonPropertyName("courierId")]
    public string CourierId { get; init; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; init; } = string.Empty;

    [JsonPropertyName("scannedAtUtc")]
    public DateTimeOffset ScannedAtUtc { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("gpsCoordinates")]
    public GpsCoordinates? GpsCoordinates { get; init; }

    [JsonPropertyName("recipientSignature")]
    public string? RecipientSignature { get; init; }
}

/// <summary>
/// Shipment filter parameters for list endpoint.
/// </summary>
public sealed class ShipmentFilter
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    [FromQuery(Name = "page")]
    [Range(1, int.MaxValue, ErrorMessage = "page must be 1 or greater")]
    public int Page { get; init; } = 1;

    [FromQuery(Name = "pageSize")]
    [Range(1, MaxPageSize, ErrorMessage = "pageSize must be between 1 and 50")]
    public int PageSize { get; init; } = DefaultPageSize;

    [FromQuery(Name = "status")]
    public ShipmentStatus? Status { get; init; }

    [FromQuery(Name = "city")]
    public string? City { get; init; }

    [FromQuery(Name = "courierId")]
    public string? CourierId { get; init; }

    [FromQuery(Name = "fromDate")]
    public DateTimeOffset? FromDate { get; init; }

    [FromQuery(Name = "toDate")]
    public DateTimeOffset? ToDate { get; init; }
}