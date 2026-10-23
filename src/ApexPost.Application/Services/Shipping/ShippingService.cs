
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services.Shipping;

public sealed class ShippingService
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IReadOnlyCollection<ShippingPricingRule> _pricingRules;
    private readonly decimal _maximumWeightKg;

    public ShippingService(
        IShipmentRepository shipmentRepository,
        IEnumerable<ShippingPricingRule> pricingRules,
        decimal maximumWeightKg = 30m)
    {
        ArgumentNullException.ThrowIfNull(shipmentRepository);
        ArgumentNullException.ThrowIfNull(pricingRules);

        if (maximumWeightKg <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumWeightKg),
                "Maximum parcel weight must be positive.");
        }

        _shipmentRepository = shipmentRepository;
        _pricingRules = pricingRules.ToArray();
        _maximumWeightKg = maximumWeightKg;
    }

    public Money CalculatePrice(
        string destinationZone,
        ShippingServiceLevel serviceLevel,
        Weight weight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZone);
        ArgumentNullException.ThrowIfNull(weight);

        if (weight.Kilograms > _maximumWeightKg)
        {
            throw new InvalidOperationException(
                $"Parcel weight cannot exceed {_maximumWeightKg} kg.");
        }

        var rule = _pricingRules.FirstOrDefault(
            candidate =>
                string.Equals(
                    candidate.DestinationZone,
                    destinationZone.Trim(),
                    StringComparison.OrdinalIgnoreCase)
                && candidate.ServiceLevel == serviceLevel);

        if (rule is null)
        {
            throw new InvalidOperationException(
                "No shipping pricing rule exists for the selected zone and service level.");
        }

        return rule.CalculatePrice(weight);
    }

    public async Task<Shipment> CreateShipmentAsync(
        string id,
        string itemType,
        decimal weightKg,
        decimal lengthCm,
        decimal widthCm,
        decimal heightCm,
        string destinationZone,
        ShippingServiceLevel serviceLevel,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemType);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZone);
        cancellationToken.ThrowIfCancellationRequested();

        var weight = new Weight(weightKg);

        _ = CalculatePrice(destinationZone, serviceLevel, weight);

        var shipment = new Shipment(
            id.Trim(),
            itemType.Trim(),
            weight,
            lengthCm,
            widthCm,
            heightCm,
            destinationZone.Trim(),
            serviceLevel);

        await _shipmentRepository.AddAsync(
            shipment,
            cancellationToken);

        return shipment;
    }
}
