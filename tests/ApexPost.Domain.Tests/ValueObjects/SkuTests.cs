using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.ValueObjects;

public class SkuTests
{
    [Fact]
    public void Constructor_ShouldNormalizeSku()
    {
        var sku = new Sku(" pack001 ");
        Assert.Equal("PACK001", sku.Value);
    }

    [Fact]
    public void Constructor_ShouldRejectEmptySku()
    {
        Assert.Throws<ArgumentException>(() => new Sku(" "));
    }

    [Fact]
    public void Constructor_ShouldRejectSpecialCharacters()
    {
        Assert.Throws<ArgumentException>(() => new Sku("PACK@001"));
    }

    [Fact]
    public void Constructor_ShouldRejectSkuLongerThanTwentyCharacters()
    {
        Assert.Throws<ArgumentException>(() => new Sku("ABCDEFGHIJKLMNOPQRSTU"));
    }

    [Fact]
    public void Equality_SameValue_ShouldBeEqual()
    {
        var s1 = new Sku("ABC123");
        var s2 = new Sku("ABC123");
        Assert.Equal(s1, s2);
        Assert.True(s1 == s2);
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        var s1 = new Sku("ABC123");
        var s2 = new Sku("ABC123");
        Assert.Equal(s1.GetHashCode(), s2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var s = new Sku("ABC123");
        Assert.Equal("ABC123", s.ToString());
    }

    [Theory]
    [InlineData("ABC123")]
    [InlineData("PACK001")]
    [InlineData("SKU001")]
    [InlineData("PRODUCT01")]
    [InlineData("A1")]
    public void Constructor_ValidFormats_ShouldAccept(string sku)
    {
        var s = new Sku(sku);
        Assert.Equal(sku.ToUpperInvariant().Trim(), s.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A@B")]
    [InlineData("A B")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public void Constructor_InvalidFormats_ShouldReject(string sku)
    {
        Assert.Throws<ArgumentException>(() => new Sku(sku));
    }
}