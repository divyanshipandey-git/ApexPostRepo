
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.ValueObjects;

public class WeightTests
{
    [Fact]
    public void Constructor_ShouldAcceptValidWeight()
    {
        var weight = new Weight(2.5m);

        Assert.Equal(2.5m, weight.Kilograms);
    }

    [Fact]
    public void Constructor_ShouldAcceptZeroWeight()
    {
        var weight = new Weight(0m);

        Assert.Equal(0m, weight.Kilograms);
    }

    [Fact]
    public void Constructor_ShouldRejectNegativeWeight()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Weight(-1m));
    }

    [Fact]
    public void ToString_ShouldIncludeKilograms()
    {
        var weight = new Weight(2.5m);

        Assert.Equal("2.5 kg", weight.ToString());
    }
}
