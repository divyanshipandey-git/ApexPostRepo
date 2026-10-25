
namespace ApexPost.Domain.ValueObjects;

public sealed record Sku
{
    public string Value { get; }

    public Sku(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        value = value.Trim().ToUpperInvariant();

        if (value.Length > 20 || !value.All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException(
                "SKU must contain only letters and digits, with a maximum length of 20.",
                nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
