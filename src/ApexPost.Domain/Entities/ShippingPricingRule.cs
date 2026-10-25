
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class ShippingPricingRule
{
    private readonly Func<Weight, Money> _pricingPolicy;

    public string DestinationZone { get; }
    public ShippingServiceLevel ServiceLevel { get; }
    public decimal PricePerKilogram { get; }
    public Money BasePrice { get; }

    public ShippingPricingRule(
        string destinationZone,
        ShippingServiceLevel serviceLevel,
        decimal pricePerKilogram,
        Money basePrice,
        Func<Weight, Money>? pricingPolicy = null,
        decimal priceMultiplier = 1m)
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

        if (priceMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceMultiplier));
        }

        DestinationZone = destinationZone.Trim();
        ServiceLevel = serviceLevel;
        PricePerKilogram = pricePerKilogram;
        BasePrice = basePrice;
        _pricingPolicy = pricingPolicy ?? (weight => CalculateLinearPrice(weight, priceMultiplier));
    }

    public Money CalculatePrice(Weight weight)
    {
        ArgumentNullException.ThrowIfNull(weight);

        return _pricingPolicy(weight)
            ?? throw new InvalidOperationException("The configured shipping pricing policy returned no price.");
    }

    private Money CalculateLinearPrice(Weight weight, decimal multiplier) =>
        new((BasePrice.Amount + PricePerKilogram * weight.Kilograms) * multiplier);
}
