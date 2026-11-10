using System;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for Weight value object
/// </summary>
public class WeightValueObjectTests
{
    [Fact]
    public void Weight_ValidValues_ShouldCreate()
    {
        // Act
        var weight = new Weight(2.5m);

        // Assert
        Assert.Equal(2.5m, weight.Kilograms);
    }

    [Fact]
    public void Weight_ZeroWeight_ShouldAccept()
    {
        // Act
        var weight = new Weight(0m);

        // Assert
        Assert.Equal(0m, weight.Kilograms);
    }

    [Fact]
    public void Weight_NegativeWeight_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(-1m));
    }

    [Fact]
    public void Weight_ToString_ShouldIncludeKilograms()
    {
        // Act
        var weight = new Weight(2.5m);

        // Assert
        Assert.Equal("2.5 kg", weight.ToString());
    }

    [Fact]
    public void Weight_Equality_DifferentInstancesSameWeight_ShouldBeEqual()
    {
        var w1 = new Weight(15.5m);
        var w2 = new Weight(15.5m);
        Assert.Equal(w1, w2);
        Assert.True(w1 == w2);
    }

    [Fact]
    public void Weight_GetHashCode_SameWeight_ReturnsSameHash()
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
    public void Weight_Constructor_VariousValidWeights_ShouldAccept(decimal weight)
    {
        var w = new Weight(weight);
        Assert.Equal(weight, w.Kilograms);
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    [InlineData(-10.0)]
    public void Weight_Constructor_VariousNegativeWeights_ShouldReject(decimal weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(weight));
    }
}