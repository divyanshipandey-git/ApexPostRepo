
namespace ApexPost.Domain.ValueObjects;

public sealed record BranchCode
{
    public string Value { get; }

    public BranchCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        value = value.Trim().ToUpperInvariant();

        if (value.Length > 10 || !value.All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException(
                "Branch code must contain only letters and digits, with a maximum length of 10.",
                nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
