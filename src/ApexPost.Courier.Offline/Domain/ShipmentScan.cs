using System.Text.Json.Serialization;

namespace ApexPost.Courier.Offline.Domain;

/// <summary>
/// Represents the type of scan performed on a shipment.
/// </summary>
public enum ScanType
{
    Pickup = 0,
    InTransit = 1,
    Delivered = 2,
    Failed = 3
}

/// <summary>
/// Represents the synchronization status of a scan document.
/// </summary>
public enum SyncStatus
{
    Pending = 0,
    Synced = 1,
    Failed = 2
}

/// <summary>
/// Domain entity for a shipment scan record stored in Couchbase Lite.
/// </summary>
public sealed class ShipmentScan
{
    /// <summary>
    /// Gets the document ID in format scan::{Guid}.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the unique tracking number for the shipment.
    /// </summary>
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type of scan performed.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ScanType ScanType { get; init; }

    /// <summary>
    /// Gets the identifier of the courier who performed the scan.
    /// </summary>
    public string CourierId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the location where the scan was performed.
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC timestamp when the scan was performed.
    /// </summary>
    public DateTimeOffset ScannedAtUtc { get; init; }

    /// <summary>
    /// Gets optional notes about the scan.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Gets the synchronization status with the server.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;

    /// <summary>
    /// Creates a new ShipmentScan with a generated document ID.
    /// </summary>
    public static ShipmentScan Create(
        string trackingNumber,
        ScanType scanType,
        string courierId,
        string location,
        DateTimeOffset scannedAtUtc,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(courierId);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        return new ShipmentScan
        {
            Id = $"scan::{Guid.NewGuid()}",
            TrackingNumber = trackingNumber.Trim(),
            ScanType = scanType,
            CourierId = courierId.Trim(),
            Location = location.Trim(),
            ScannedAtUtc = scannedAtUtc,
            Notes = notes?.Trim(),
            SyncStatus = SyncStatus.Pending
        };
    }
}