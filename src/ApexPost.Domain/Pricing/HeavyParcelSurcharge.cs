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

        return 0m;
    }
}
