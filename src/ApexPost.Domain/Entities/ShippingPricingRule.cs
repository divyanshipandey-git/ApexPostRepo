
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class ShippingPricingRule
{
    public string DestinationZone { get; }
    public ShippingServiceLevel ServiceLevel { get; }
    public decimal PricePerKilogram { get; }
    public Money BasePrice { get; }

    public ShippingPricingRule(
        string destinationZone,
        ShippingServiceLevel serviceLevel,
        decimal pricePerKilogram,
        Money basePrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZone);
        ArgumentNullException.ThrowIfNull(basePrice);

        if (!Enum.IsDefined(serviceLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(serviceLevel));
        }

        if (pricePerKilogram < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pricePerKilogram),
                "Price per kilogram cannot be negative.");
        }

        if (basePrice.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(basePrice),
                "Base price cannot be negative.");
        }

        DestinationZone = destinationZone.Trim();
        ServiceLevel = serviceLevel;
        PricePerKilogram = pricePerKilogram;
        BasePrice = basePrice;
    }

    public Money CalculatePrice(Weight weight)
    {
        ArgumentNullException.ThrowIfNull(weight);

        var total = BasePrice.Amount
            + (PricePerKilogram * weight.Kilograms);

        return new Money(total);
    }
}
