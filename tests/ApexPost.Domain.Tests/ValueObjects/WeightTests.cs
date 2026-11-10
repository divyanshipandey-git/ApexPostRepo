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
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(-1m));
    }

    [Fact]
    public void ToString_ShouldIncludeKilograms()
    {
        var weight = new Weight(2.5m);
        Assert.Equal("2.5 kg", weight.ToString());
    }

    [Fact]
    public void Equality_DifferentInstancesSameWeight_ShouldBeEqual()
    {
        var w1 = new Weight(15.5m);
        var w2 = new Weight(15.5m);
        Assert.Equal(w1, w2);
        Assert.True(w1 == w2);
    }

    [Fact]
    public void GetHashCode_SameWeight_ReturnsSameHash()
    {
        var w1 = new Weight(15.5m);
        var w2 = new Weight(15.5m);
        Assert.Equal(w1.GetHashCode(), w2.GetHashCode());
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(0.01)]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(10.0)]
    [InlineData(100.0)]
    [InlineData(1000.0)]
    public void Constructor_VariousValidWeights_ShouldAccept(decimal weight)
    {
        var w = new Weight(weight);
        Assert.Equal(weight, w.Kilograms);
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    [InlineData(-10.0)]
    public void Constructor_VariousNegativeWeights_ShouldReject(decimal weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(weight));
    }
}