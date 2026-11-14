namespace ApexPost.Domain.Pricing;

/// <summary>Surcharge for heavy parcels.</summary>
public static class HeavyParcelSurcharge
{
    public static decimal Calculate(decimal weightKg)
    {
        if (weightKg <= 0 || weightKg > 70)
        {
            throw new ArgumentOutOfRangeException(nameof(weightKg));
        }

        if (weightKg > 30)
        {
            return Math.Round(12.50m + (weightKg - 30m) * 0.75m, 2, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }
}
