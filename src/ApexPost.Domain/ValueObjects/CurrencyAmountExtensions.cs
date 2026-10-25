namespace ApexPost.Domain.ValueObjects;

/// <summary>Shared currency precision rules for monetary amounts.</summary>
public static class CurrencyAmountExtensions
{
    extension (decimal amount)
    {
        public decimal ToCurrencyPrecision() =>
            decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
