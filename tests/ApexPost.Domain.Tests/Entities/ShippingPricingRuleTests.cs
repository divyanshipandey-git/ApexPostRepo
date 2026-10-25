
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public sealed class ShippingPricingRuleTests
{
    [Fact]
    public void Constructor_ValidValues_CreatesRule()
    {
        var rule = new ShippingPricingRule(
            "ZONE1",
            ShippingServiceLevel.Standard,
            10m,
            new Money(20m));

        Assert.Equal("ZONE1", rule.DestinationZone);
        Assert.Equal(10m, rule.PricePerKilogram);
        Assert.Equal(20m, rule.BasePrice.Amount);
    }

    [Fact]
    public void CalculatePrice_ValidWeight_ReturnsBasePricePlusWeightCost()
    {
        var rule = new ShippingPricingRule(
            "ZONE1",
            ShippingServiceLevel.Standard,
            10m,
            new Money(20m));

        var result = rule.CalculatePrice(new Weight(2.5m));

        Assert.Equal(45m, result.Amount);
    }

    [Fact]
    public void CalculatePrice_UsesConfiguredDelegatePolicy()
    {
        var rule = new ShippingPricingRule(
            "ZONE1",
            ShippingServiceLevel.Standard,
            10m,
            new Money(20m),
            weight => new Money(20m + 10m * weight.Kilograms));

        Assert.Equal(45m, rule.CalculatePrice(new Weight(2.5m)).Amount);
    }

    [Fact]
    public void CalculatePrice_DifferentDelegatePoliciesProduceDifferentPrices()
    {
        var regular = new ShippingPricingRule(
            "ZONE1",
            ShippingServiceLevel.Standard,
            10m,
            new Money(20m));
        var discounted = new ShippingPricingRule(
            "ZONE1",
            ShippingServiceLevel.Standard,
            10m,
            new Money(20m),
            weight => new Money((20m + 10m * weight.Kilograms) * 0.9m));

        Assert.Equal(45m, regular.CalculatePrice(new Weight(2.5m)).Amount);
        Assert.Equal(40.5m, discounted.CalculatePrice(new Weight(2.5m)).Amount);
    }

    [Fact]
    public void Constructor_NegativePricePerKilogram_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShippingPricingRule(
                "ZONE1",
                ShippingServiceLevel.Standard,
                -1m,
                new Money(20m)));
    }

    [Fact]
    public void Constructor_NegativeBasePrice_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShippingPricingRule(
                "ZONE1",
                ShippingServiceLevel.Standard,
                10m,
                new Money(-5m)));
    }

    [Fact]
    public void CalculatePrice_NullWeight_ThrowsException()
    {
        var rule = new ShippingPricingRule(
            "ZONE1",
            ShippingServiceLevel.Standard,
            10m,
            new Money(20m));

        Assert.Throws<ArgumentNullException>(() =>
            rule.CalculatePrice(null!));
    }
}
