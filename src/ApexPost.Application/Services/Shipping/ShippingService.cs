
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services.Shipping;

public sealed class ShippingService
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IReadOnlyCollection<ShippingPricingRule> _pricingRules;
    private readonly IReadOnlyDictionary<ShippingServiceLevel, decimal> _maximumWeightKgByServiceLevel;
    private readonly TimeProvider _timeProvider;

    public ShippingService(
        IShipmentRepository shipmentRepository,
        IEnumerable<ShippingPricingRule> pricingRules,
        decimal maximumWeightKg = 30m,
        TimeProvider? timeProvider = null,
        IReadOnlyDictionary<ShippingServiceLevel, decimal>? maximumWeightKgByServiceLevel = null)
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
        _maximumWeightKgByServiceLevel = maximumWeightKgByServiceLevel is null
            ? Enum.GetValues<ShippingServiceLevel>().ToDictionary(level => level, _ => maximumWeightKg)
            : new Dictionary<ShippingServiceLevel, decimal>(maximumWeightKgByServiceLevel);
        foreach (var level in Enum.GetValues<ShippingServiceLevel>())
        {
            if (!_maximumWeightKgByServiceLevel.TryGetValue(level, out var limit) || limit <= 0)
            {
                throw new ArgumentException(
                    $"A positive maximum weight is required for {level} shipping.",
                    nameof(maximumWeightKgByServiceLevel));
            }
        }
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Money CalculatePrice(
        string destinationZone,
        ShippingServiceLevel serviceLevel,
        Weight weight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZone);
        ArgumentNullException.ThrowIfNull(weight);

        if (!_maximumWeightKgByServiceLevel.TryGetValue(serviceLevel, out var maximumWeightKg))
        {
            throw new ArgumentOutOfRangeException(nameof(serviceLevel));
        }

        if (weight.Kilograms > maximumWeightKg)
        {
            throw new InvalidOperationException(
                $"Parcel weight cannot exceed {maximumWeightKg} kg for {serviceLevel} shipping.");
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
        CancellationToken cancellationToken = default,
        BranchCode? branchCode = null,
        string? counterTransactionId = null)
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
            serviceLevel,
            branchCode,
            counterTransactionId,
            _timeProvider.GetUtcNow());

        await _shipmentRepository.AddAsync(
            shipment,
            cancellationToken);

        return shipment;
    }
}
