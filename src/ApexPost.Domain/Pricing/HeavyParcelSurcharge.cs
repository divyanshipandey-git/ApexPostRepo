using ApexPost.Domain.Entities;

namespace ApexPost.Domain.Pricing;

/// <summary>
/// Business rule: parcels heavier than <see cref="ThresholdKg"/> pay a flat fee plus a rate per kilogram above the threshold.
/// A parcel of exactly <see cref="ThresholdKg"/> pays nothing.
/// </summary>
public static class HeavyParcelSurcharge
{
    /// <summary>Weight above which the surcharge applies, in kilograms.</summary>
    public const decimal ThresholdKg = 30m;

    /// <summary>Flat part of the surcharge.</summary>
    public const decimal FlatFee = 12.50m;

    /// <summary>Extra charge per kilogram above <see cref="ThresholdKg"/>.</summary>
    public const decimal RatePerKgOverThreshold = 0.75m;

    /// <summary>Calculates the surcharge for a parcel, rounded to cents (away from zero).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The weight is not positive or exceeds the maximum parcel weight.</exception>
    public static decimal Calculate(decimal weightKg)
    {
        if (weightKg <= 0 || weightKg > TrackedShipment.MaxWeightKg)
        {
            throw new ArgumentOutOfRangeException(nameof(weightKg), weightKg, "Weight must be positive and at most the maximum parcel weight.");
        }

        if (weightKg <= ThresholdKg)
        {
            return 0m;
        }

        var surcharge = FlatFee + (weightKg - ThresholdKg) * RatePerKgOverThreshold;
        return Math.Round(surcharge, 2, MidpointRounding.AwayFromZero);
    }
}
