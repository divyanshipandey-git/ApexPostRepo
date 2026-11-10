using System;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ApexPost.Courier.Console.Models;

/// <summary>
/// Local scan document model for the console application.
/// Combines properties needed for both API communication and local storage.
/// </summary>
public sealed class LocalScanDocument
{
    /// <summary>
    /// Unique scan ID from Part A offline store (format: scan::{Guid}).
    /// </summary>
    public string ScanId { get; init; } = string.Empty;

    /// <summary>
    /// Unique tracking number for the shipment.
    /// </summary>
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>
    /// Type of scan performed.
    /// </summary>
    
    public ScanType ScanType { get; init; }

    /// <summary>
    /// Courier ID who performed the scan.
    /// </summary>
    public string CourierId { get; init; } = string.Empty;

    /// <summary>
    /// Location where scan was performed.
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// UTC timestamp when scan was performed.
    /// </summary>
    public DateTimeOffset ScannedAtUtc { get; init; }

    /// <summary>
    /// Optional notes about the scan.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Optional GPS coordinates of the scan location.
    /// </summary>
    public GpsCoordinates? GpsCoordinates { get; init; }

    /// <summary>
    /// Optional recipient signature name (required for Delivered scan type).
    /// </summary>
    public string? RecipientSignature { get; init; }

    /// <summary>
    /// Synchronization status with the server.
    /// </summary>
    
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;

    /// <summary>
    /// Creates a new LocalScanDocument with a generated document ID.
    /// </summary>
    public static LocalScanDocument Create(
        string trackingNumber,
        ScanType scanType,
        string courierId,
        string location,
        DateTimeOffset scannedAtUtc,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
            throw new ArgumentException("Tracking number is required", nameof(trackingNumber));

        // Validate tracking number format: APX-NNNNNNNN
        var trimmedTrackingNumber = trackingNumber.Trim();
        if (!Regex.IsMatch(trimmedTrackingNumber, @"^APX-\d{8}$"))
            throw new ArgumentException("Tracking number must be in format APX-NNNNNNNN (8 digits, e.g., APX-20240115)", nameof(trackingNumber));


        if (string.IsNullOrWhiteSpace(courierId))
            throw new ArgumentException("Courier ID is required", nameof(courierId));
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location is required", nameof(location));

        return new LocalScanDocument
        {
            ScanId = $"scan::{Guid.NewGuid()}",
            TrackingNumber = trimmedTrackingNumber,
            ScanType = scanType,
            CourierId = courierId.Trim(),
            Location = location.Trim(),
            ScannedAtUtc = scannedAtUtc,
            Notes = notes?.Trim(),
            SyncStatus = SyncStatus.Pending
        };
    }

    /// <summary>
    /// Converts to the wire DTO used for upload.
    /// </summary>
    public ScanDocumentDto ToApiRequest()
    {
        return new ScanDocumentDto
        {
            ScanId = ScanId,
            ScanType = ScanType,
            CourierId = CourierId,
            Location = Location,
            ScannedAtUtc = ScannedAtUtc,
            Notes = Notes,
            GpsCoordinates = GpsCoordinates != null
                ? new GpsCoordinatesDto { Latitude = GpsCoordinates.Latitude, Longitude = GpsCoordinates.Longitude }
                : null,
            RecipientSignature = RecipientSignature
        };
    }

    /// <summary>
    /// Creates from offline domain model.
    /// </summary>
    public static LocalScanDocument FromOffline(ApexPost.Courier.Offline.Domain.ShipmentScan scan)
    {
        return new LocalScanDocument
        {
            ScanId = scan.Id,
            TrackingNumber = scan.TrackingNumber,
            ScanType = (ScanType)scan.ScanType,
            CourierId = scan.CourierId,
            Location = scan.Location,
            ScannedAtUtc = scan.ScannedAtUtc,
            Notes = scan.Notes,
            SyncStatus = (SyncStatus)scan.SyncStatus
        };
    }

    /// <summary>
    /// Converts to offline domain model.
    /// </summary>
    public ApexPost.Courier.Offline.Domain.ShipmentScan ToOffline()
    {
        return new ApexPost.Courier.Offline.Domain.ShipmentScan
        {
            Id = ScanId,
            TrackingNumber = TrackingNumber,
            ScanType = (ApexPost.Courier.Offline.Domain.ScanType)ScanType,
            CourierId = CourierId,
            Location = Location,
            ScannedAtUtc = ScannedAtUtc,
            Notes = Notes,
            SyncStatus = (ApexPost.Courier.Offline.Domain.SyncStatus)SyncStatus
        };
    }
}

/// <summary>
/// GPS coordinates for a scan.
/// </summary>
public sealed class GpsCoordinates
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}

/// <summary>
/// Scan type enumeration matching Part A and API.
/// </summary>

[JsonConverter(typeof(JsonStringEnumConverter<ScanType>))]
public enum ScanType
{
    Pickup = 0,
    InTransit = 1,
    Delivered = 2,
    Failed = 3
}

/// <summary>
/// Synchronization status of a scan document.
/// </summary>

[JsonConverter(typeof(JsonStringEnumConverter<SyncStatus>))]
public enum SyncStatus
{
    Pending = 0,
    Synced = 1,
    Failed = 2
}