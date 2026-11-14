namespace ApexPost.Domain.Pricing;

/// <summary>Surcharge for heavy parcels (stub created so the failing tests compile: RED step).</summary>
public static class HeavyParcelSurcharge
{
    public static decimal Calculate(decimal weightKg) => throw new NotImplementedException();
}
