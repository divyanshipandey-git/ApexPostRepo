
namespace ApexPost.Domain.ValueObjects;

public sealed record Address
{
    public string Street { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    public Address(
        string street,
        string city,
        string state,
        string postalCode,
        string country)
    {
        Street = Required(street, nameof(street));
        City = Required(city, nameof(city));
        State = Required(state, nameof(state));
        PostalCode = Required(postalCode, nameof(postalCode));
        Country = Required(country, nameof(country));
    }

    private static string Required(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    public override string ToString() =>
        $"{Street}, {City}, {State} {PostalCode}, {Country}";
}
