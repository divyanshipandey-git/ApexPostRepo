
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.ValueObjects;

public class AddressTests
{
    [Fact]
    public void Constructor_ShouldStoreAddressComponents()
    {
        var address = new Address(
            " MG Road ",
            "Indore",
            "Madhya Pradesh",
            "452001",
            "India");

        Assert.Equal("MG Road", address.Street);
        Assert.Equal("Indore", address.City);
        Assert.Equal("Madhya Pradesh", address.State);
        Assert.Equal("452001", address.PostalCode);
        Assert.Equal("India", address.Country);
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyStreet()
    {
        Assert.Throws<ArgumentException>(() =>
            new Address("", "Indore", "MP", "452001", "India"));
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyCity()
    {
        Assert.Throws<ArgumentException>(() =>
            new Address("MG Road", " ", "MP", "452001", "India"));
    }

    [Fact]
    public void ToString_ShouldFormatAddress()
    {
        var address = new Address(
            "MG Road", "Indore", "MP", "452001", "India");

        Assert.Equal(
            "MG Road, Indore, MP 452001, India",
            address.ToString());
    }
}
