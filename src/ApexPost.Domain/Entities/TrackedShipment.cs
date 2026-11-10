using ApexPost.Domain.ValueObjects;
using System.Text.Json.Serialization;

namespace ApexPost.Domain.Entities;

/// <summary>
/// Represents a tracked shipment for the API (different from the shipping service Shipment entity).
/// </summary>
public sealed class TrackedShipment
{
    private readonly List<TrackedShipmentScan> _scans = [];

    /// <summary>
    /// Gets the unique identifier for the tracked shipment.
    /// </summary>
    public string Id { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the tracking number in format APX-NNNNNNNN.
    /// </summary>
    public string TrackingNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the current status of the shipment.
    /// </summary>
    public ShipmentStatus Status { get; private set; } = ShipmentStatus.Created;

    /// <summary>
    /// Gets the package weight in kilograms.
    /// </summary>
    public decimal WeightKg { get; private set; }

    /// <summary>
    /// Gets the recipient's full name.
    /// </summary>
    public string RecipientName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the recipient's phone number in E.164 format.
    /// </summary>
    public string RecipientPhone { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the recipient's street address.
    /// </summary>
    public string RecipientAddress { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the recipient's city.
    /// </summary>
    public string RecipientCity { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the recipient's postal code.
    /// </summary>
    public string? RecipientPostalCode { get; private set; }

    /// <summary>
    /// Gets the recipient's country code (ISO 3166-1 alpha-2).
    /// </summary>
    public string RecipientCountry { get; private set; } = "US";

    /// <summary>
    /// Gets optional special handling instructions.
    /// </summary>
    public string? SpecialInstructions { get; private set; }

    /// <summary>
    /// Gets optional declared value for insurance.
    /// </summary>
    public decimal? DeclaredValue { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the shipment was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the shipment was last updated.
    /// </summary>
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Gets the collection of scans for this shipment.
    /// </summary>
    public IReadOnlyCollection<TrackedShipmentScan> Scans => _scans.AsReadOnly();

    // Private constructor for EF Core
    private TrackedShipment()
    {
        Id = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Creates a new tracked shipment.
    /// </summary>
    public static TrackedShipment Create(
        string trackingNumber,
        decimal weightKg,
        string recipientName,
        string recipientPhone,
        string recipientAddress,
        string recipientCity,
        string? recipientPostalCode = null,
        string recipientCountry = "US",
        string? specialInstructions = null,
        decimal? declaredValue = null,
        DateTimeOffset? createdAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);
        if (!System.Text.RegularExpressions.Regex.IsMatch(trackingNumber, @"^APX-\d{8}$"))
        {
            throw new ArgumentException("Tracking number must match format APX-NNNNNNNN", nameof(trackingNumber));
        }

        if (weightKg <= 0 || weightKg > 70)
        {
            throw new ArgumentOutOfRangeException(nameof(weightKg), "Weight must be between 0.1 and 70 kg");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientPhone);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientCity);

        var shipment = new TrackedShipment
        {
            Id = Guid.NewGuid().ToString("N"),
            TrackingNumber = trackingNumber.Trim(),
            WeightKg = weightKg,
            RecipientName = recipientName.Trim(),
            RecipientPhone = recipientPhone.Trim(),
            RecipientAddress = recipientAddress.Trim(),
            RecipientCity = recipientCity.Trim(),
            RecipientPostalCode = recipientPostalCode?.Trim(),
            RecipientCountry = recipientCountry ?? "US",
            SpecialInstructions = specialInstructions?.Trim(),
            DeclaredValue = declaredValue,
            Status = ShipmentStatus.Created,
            CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow
        };

        return shipment;
    }

    /// <summary>
    /// Updates the editable details of the shipment.
    /// Only allowed while status is Created.
    /// </summary>
    public void UpdateDetails(
        decimal? weightKg = null,
        string? recipientName = null,
        string? recipientPhone = null,
        string? recipientAddress = null,
        string? recipientCity = null,
        string? recipientPostalCode = null,
        string? recipientCountry = null,
        string? specialInstructions = null,
        decimal? declaredValue = null)
    {
        if (Status != ShipmentStatus.Created)
        {
            throw new InvalidOperationException("Shipment can only be updated while in Created status");
        }

        if (weightKg.HasValue)
        {
            if (weightKg.Value <= 0 || weightKg.Value > 70)
            {
                throw new ArgumentOutOfRangeException(nameof(weightKg), "Weight must be between 0.1 and 70 kg");
            }
            WeightKg = weightKg.Value;
        }

        if (!string.IsNullOrWhiteSpace(recipientName))
        {
            RecipientName = recipientName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(recipientPhone))
        {
            RecipientPhone = recipientPhone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(recipientAddress))
        {
            RecipientAddress = recipientAddress.Trim();
        }

        if (!string.IsNullOrWhiteSpace(recipientCity))
        {
            RecipientCity = recipientCity.Trim();
        }

        if (!string.IsNullOrWhiteSpace(recipientPostalCode))
        {
            RecipientPostalCode = recipientPostalCode.Trim();
        }

        if (!string.IsNullOrWhiteSpace(recipientCountry))
        {
            RecipientCountry = recipientCountry.Trim();
        }

        if (!string.IsNullOrWhiteSpace(specialInstructions))
        {
            SpecialInstructions = specialInstructions.Trim();
        }

        if (declaredValue.HasValue)
        {
            if (declaredValue.Value < 0 || declaredValue.Value > 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(declaredValue), "Declared value must be between 0 and 10000");
            }
            DeclaredValue = declaredValue.Value;
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates the shipment status with validation.
    /// </summary>
    public void UpdateStatus(
        ShipmentStatus newStatus,
        string? reason = null,
        string? recipientSignature = null,
        double? latitude = null,
        double? longitude = null)
    {
        if (!IsValidTransition(Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Invalid status transition from {Status} to {newStatus}. " +
                $"Allowed: {string.Join(", ", GetAllowedTransitions(Status))}");
        }

        if (newStatus == ShipmentStatus.Delivered && string.IsNullOrWhiteSpace(recipientSignature))
        {
            throw new InvalidOperationException("Recipient signature is required for Delivered status");
        }

        if (newStatus == ShipmentStatus.Failed && string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Reason is required for Failed status");
        }

        var scan = new TrackedShipmentScan
        {
            Id = Guid.NewGuid().ToString("N"),
            TrackedShipmentId = Id,
            ScanType = MapToScanType(newStatus),
            CourierId = "SYSTEM",
            Location = (latitude.HasValue && longitude.HasValue)
                ? $"{latitude.Value},{longitude.Value}"
                : "Status update",
            ScannedAtUtc = DateTimeOffset.UtcNow,
            Notes = reason,
            RecipientSignature = recipientSignature,
            Latitude = latitude,
            Longitude = longitude
        };

        _scans.Add(scan);
        Status = newStatus;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Adds a scan from courier upload.
    /// </summary>
    public void AddScan(TrackedShipmentScan scan)
    {
        _scans.Add(scan);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static bool IsValidTransition(ShipmentStatus from, ShipmentStatus to)
    {
        var allowed = GetAllowedTransitions(from);
        return allowed.Contains(to);
    }

    private static IReadOnlyList<ShipmentStatus> GetAllowedTransitions(ShipmentStatus status)
    {
        return status switch
        {
            ShipmentStatus.Created => [ShipmentStatus.PickedUp, ShipmentStatus.Failed],
            ShipmentStatus.PickedUp => [ShipmentStatus.InTransit, ShipmentStatus.Failed],
            ShipmentStatus.InTransit => [ShipmentStatus.OutForDelivery, ShipmentStatus.Delivered, ShipmentStatus.Failed],
            ShipmentStatus.OutForDelivery => [ShipmentStatus.Delivered, ShipmentStatus.Failed],
            ShipmentStatus.Delivered => [],
            ShipmentStatus.Failed => [],
            _ => []
        };
    }

    private static ScanType MapToScanType(ShipmentStatus status) => status switch
    {
        ShipmentStatus.PickedUp => ScanType.Pickup,
        ShipmentStatus.InTransit => ScanType.InTransit,
        ShipmentStatus.OutForDelivery => ScanType.InTransit,
        ShipmentStatus.Delivered => ScanType.Delivered,
        ShipmentStatus.Failed => ScanType.Failed,
        _ => ScanType.InTransit
    };
}

/// <summary>
/// Shipment status enumeration for tracked shipments.
/// </summary>
public enum ShipmentStatus
{
    Created = 0,
    PickedUp = 1,
    InTransit = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Failed = 5
}

/// <summary>
/// Scan type for tracked shipments.
/// </summary>
public enum ScanType
{
    Pickup = 0,
    InTransit = 1,
    Delivered = 2,
    Failed = 3
}

/// <summary>
/// Represents a scan event for a tracked shipment.
/// </summary>
public sealed class TrackedShipmentScan
{
    public string Id { get; init; } = string.Empty;
    public string TrackedShipmentId { get; init; } = string.Empty;
    public ScanType ScanType { get; init; }
    public string CourierId { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public DateTimeOffset ScannedAtUtc { get; init; }
    public string? Notes { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? RecipientSignature { get; init; }
}