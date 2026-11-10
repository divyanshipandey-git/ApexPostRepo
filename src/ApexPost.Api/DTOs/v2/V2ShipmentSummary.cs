using System.Text.Json.Serialization;
using ApexPost.Api.DTOs;

namespace ApexPost.Api.DTOs.v2;

/// <summary>
/// v2 Shipment summary for list responses with status as string enum.
/// </summary>
public sealed class V2ShipmentSummary
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
    /// Nested recipient information object (summary).
    /// </summary>
    [JsonPropertyName("recipient")]
    public V2RecipientInfo Recipient { get; init; } = new();

    /// <summary>
    /// Package weight in kilograms.
    /// </summary>
    [JsonPropertyName("weightKg")]
    public decimal WeightKg { get; init; }

    /// <summary>
    /// UTC timestamp when the shipment was created.
    /// </summary>
    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// UTC timestamp of the most recent scan.
    /// </summary>
    [JsonPropertyName("lastScanAtUtc")]
    public DateTimeOffset? LastScanAtUtc { get; init; }

    /// <summary>
    /// Total number of scans for this shipment.
    /// </summary>
    [JsonPropertyName("scanCount")]
    public int ScanCount { get; init; }

    /// <summary>
    /// Summary of the most recent scan.
    /// </summary>
    [JsonPropertyName("latestScan")]
    public V2LatestScanSummary? LatestScan { get; init; }
}