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

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a1 = new Address("123 Main St", "City", "ST", "12345", "USA");
        var a2 = new Address("123 Main St", "City", "ST", "12345", "USA");
        Assert.Equal(a1, a2);
        Assert.True(a1 == a2);
    }

    [Fact]
    public void GetHashCode_SameValues_ShouldBeEqual()
    {
        var a1 = new Address("123 Main St", "City", "ST", "12345", "USA");
        var a2 = new Address("123 Main St", "City", "ST", "12345", "USA");
        Assert.Equal(a1.GetHashCode(), a2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentStreet_ShouldNotBeEqual()
    {
        var a1 = new Address("123 Main St", "City", "ST", "12345", "USA");
        var a2 = new Address("456 Oak Ave", "City", "ST", "12345", "USA");
        Assert.NotEqual(a1, a2);
    }

    [Fact]
    public void Constructor_TrimsWhitespace()
    {
        var address = new Address("  Main St  ", "  City  ", "  ST  ", "  12345  ", "  USA  ");
        Assert.Equal("Main St", address.Street);
        Assert.Equal("City", address.City);
        Assert.Equal("ST", address.State);
        Assert.Equal("12345", address.PostalCode);
        Assert.Equal("USA", address.Country);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_RejectsEmptyState(string state)
    {
        Assert.Throws<ArgumentException>(() =>
            new Address("MG Road", "Indore", state, "452001", "India"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_RejectsEmptyPostalCode(string postalCode)
    {
        Assert.Throws<ArgumentException>(() =>
            new Address("MG Road", "Indore", "MP", postalCode, "India"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_RejectsEmptyCountry(string country)
    {
        Assert.Throws<ArgumentException>(() =>
            new Address("MG Road", "Indore", "MP", "452001", country));
    }
}