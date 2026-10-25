
namespace ApexPost.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }

    public string Currency => "INR";

    public Money(decimal amount)
    {
        Amount = amount.ToCurrencyPrecision();
    }

    public static Money Zero => new(0m);

    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Money(Amount + other.Amount);
    }

    public Money Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Money(Amount - other.Amount);
    }
}
