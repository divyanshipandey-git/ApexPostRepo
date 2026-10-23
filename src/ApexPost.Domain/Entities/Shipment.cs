
using ApexPost.Domain.ValueObjects;

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

    public Shipment(
        string id,
        string itemType,
        Weight weight,
        decimal lengthCm,
        decimal widthCm,
        decimal heightCm,
        string destinationZone,
        ShippingServiceLevel serviceLevel)
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

        Id = id.Trim();
        ItemType = itemType;
        Weight = weight;
        LengthCm = lengthCm;
        WidthCm = widthCm;
        HeightCm = heightCm;
        DestinationZone = destinationZone.Trim();
        ServiceLevel = serviceLevel;
    }
}
