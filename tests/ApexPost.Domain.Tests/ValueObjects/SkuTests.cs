
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
        Assert.Throws<ArgumentException>(
            () => new Sku(" "));
    }

    [Fact]
    public void Constructor_ShouldRejectSpecialCharacters()
    {
        Assert.Throws<ArgumentException>(
            () => new Sku("PACK-001"));
    }

    [Fact]
    public void Constructor_ShouldRejectSkuLongerThanTwentyCharacters()
    {
        Assert.Throws<ArgumentException>(
            () => new Sku("ABCDEFGHIJKLMNOPQRSTU"));
    }
}
