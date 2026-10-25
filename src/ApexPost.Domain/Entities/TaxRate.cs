
namespace ApexPost.Domain.Entities;

public sealed class TaxRate
{
    public TaxCategory Category { get; }
    public decimal Rate { get; }

    public TaxRate(TaxCategory category, decimal rate)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        if (rate is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rate),
                "Tax rate must be between 0 and 1.");
        }

        Category = category;
        Rate = rate;
    }

    public decimal CalculateTax(decimal taxableAmount)
    {
        if (taxableAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(taxableAmount));
        }

        return decimal.Round(
            taxableAmount * Rate,
            2,
            MidpointRounding.AwayFromZero);
    }

   
}
