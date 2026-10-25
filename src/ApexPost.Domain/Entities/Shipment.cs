
using ApexPost.Domain.ValueObjects;
using System.Text.Json.Serialization;

namespace ApexPost.Domain.Entities;

public sealed class Shipment
{
    public string Id { get; }
    public string ItemType { get; }
    public Weight Weight { get; }
    public decimal LengthCm { get; }
    public decimal WidthCm { get; }
    public decimal HeightCm { get; }
    public string DestinationZone { get; }
    public ShippingServiceLevel ServiceLevel { get; }
    public BranchCode? BranchCode { get; }
    public string? CounterTransactionId { get; }

    [JsonInclude]
    public string? CourierStatus { get; private set; }

    [JsonInclude]
    public DateTimeOffset? CourierStatusUpdatedAtUtc { get; private set; }

    [JsonInclude]
    public DateTimeOffset? CreatedAtUtc { get; private set; }

    public Shipment(
        string id,
        string itemType,
        Weight weight,
        decimal lengthCm,
        decimal widthCm,
        decimal heightCm,
        string destinationZone,
        ShippingServiceLevel serviceLevel,
        BranchCode? branchCode = null,
        string? counterTransactionId = null,
        DateTimeOffset? createdAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemType);
        ArgumentNullException.ThrowIfNull(weight);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZone);

        if (itemType is not ("Parcel" or "Letter"))
        {
            throw new ArgumentException(
                "Item type must be Parcel or Letter.",
                nameof(itemType));
        }

        if (lengthCm <= 0 || widthCm <= 0 || heightCm <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lengthCm),
                "All dimensions must be positive.");
        }

        if (!Enum.IsDefined(serviceLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(serviceLevel));
        }

        if (counterTransactionId is { Length: > 128 } || counterTransactionId?.Any(char.IsControl) == true)
        {
            throw new ArgumentException("Transaction reference is invalid.", nameof(counterTransactionId));
        }

        Id = id.Trim();
        ItemType = itemType;
        Weight = weight;
        LengthCm = lengthCm;
        WidthCm = widthCm;
        HeightCm = heightCm;
        DestinationZone = destinationZone.Trim();
        ServiceLevel = serviceLevel;
        BranchCode = branchCode;
        CounterTransactionId = string.IsNullOrWhiteSpace(counterTransactionId)
            ? null
            : counterTransactionId.Trim();
        if (createdAtUtc.HasValue && createdAtUtc.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Shipment timestamps must be UTC.", nameof(createdAtUtc));
        }

        CreatedAtUtc = createdAtUtc;
    }

    public void RecordCourierStatus(string status, DateTimeOffset receivedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        var normalizedStatus = status.Trim();
        if (normalizedStatus.Length > 100 || normalizedStatus.Any(char.IsControl))
        {
            throw new ArgumentException("Courier status must be at most 100 printable characters.", nameof(status));
        }

        if (receivedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Courier status timestamps must be UTC.", nameof(receivedAtUtc));
        }

        CourierStatus = normalizedStatus;
        CourierStatusUpdatedAtUtc = receivedAtUtc;
    }
}
