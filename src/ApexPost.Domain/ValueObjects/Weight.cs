
namespace ApexPost.Domain.ValueObjects;

public sealed record Weight
{
    public decimal Kilograms { get; }

    public Weight(decimal kilograms)
    {
        if (kilograms < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kilograms),
                "Weight cannot be negative.");
        }

        Kilograms = kilograms;
    }

    public override string ToString() => $"{Kilograms} kg";
}
