using System;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

/// <summary>
/// Tests for ShippingPricingRule entity
/// </summary>
public class ShippingPricingRuleEntityTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesRule()
    {
        // Act
        var rule = new ShippingPricingRule(
            "US-DOMESTIC",
            ShippingServiceLevel.Standard,
            1.50m,
            new Money(5.00m));

        // Assert
        Assert.Equal("US-DOMESTIC", rule.DestinationZone);
        Assert.Equal(ShippingServiceLevel.Standard, rule.ServiceLevel);
        Assert.Equal(1.50m, rule.PricePerKilogram);
        Assert.Equal(new Money(5.00m), rule.BasePrice);
    }

    [Fact]
    public void Constructor_EmptyDestinationZone_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ShippingPricingRule(
            "", ShippingServiceLevel.Standard, 1.50m, new Money(5.00m)));
    }

    [Fact]
    public void Constructor_NullBasePrice_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ShippingPricingRule(
            "US-DOMESTIC", ShippingServiceLevel.Standard, 1.50m, null!));
    }

    [Fact]
    public void Constructor_NegativePricePerKg_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShippingPricingRule(
            "US-DOMESTIC", ShippingServiceLevel.Standard, -1.00m, new Money(5.00m)));
    }

    [Fact]
    public void Constructor_NegativeBasePrice_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShippingPricingRule(
            "US-DOMESTIC", ShippingServiceLevel.Standard, 1.50m, new Money(-5.00m)));
    }

    [Fact]
    public void Constructor_ZeroPriceMultiplier_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShippingPricingRule(
            "US-DOMESTIC", ShippingServiceLevel.Standard, 1.50m, new Money(5.00m), priceMultiplier: 0m));
    }

    [Fact]
    public void CalculatePrice_StandardWeight_CalculatesCorrectly()
    {
        // Arrange
        var rule = new ShippingPricingRule(
            "US-DOMESTIC",
            ShippingServiceLevel.Standard,
            1.50m,
            new Money(5.00m));

        // Act
        var price = rule.CalculatePrice(new Weight(2.0m));

        // Assert: 5.00 + (1.50 * 2.0) = 8.00
        Assert.Equal(new Money(8.00m), price);
    }

    [Fact]
    public void CalculatePrice_WithCustomPricingPolicy_UsesCustomPolicy()
    {
        // Arrange
        var rule = new ShippingPricingRule(
            "US-DOMESTIC",
            ShippingServiceLevel.Standard,
            1.50m,
            new Money(5.00m),
            weight => new Money(weight.Kilograms * 3.00m)); // Custom: $3/kg

        // Act
        var price = rule.CalculatePrice(new Weight(2.0m));

        // Assert: 2.0 * 3.00 = 6.00
        Assert.Equal(new Money(6.00m), price);
    }

    [Fact]
    public void CalculatePrice_WithPriceMultiplier_AppliesMultiplier()
    {
        // Arrange
        var rule = new ShippingPricingRule(
            "US-DOMESTIC",
            ShippingServiceLevel.Standard,
            1.50m,
            new Money(5.00m),
            priceMultiplier: 2.0m);

        // Act
        var price = rule.CalculatePrice(new Weight(2.0m));

        // Assert: (5.00 + 1.50 * 2.0) * 2.0 = 8.00 * 2.0 = 16.00
        Assert.Equal(new Money(16.00m), price);
    }

    [Fact]
    public void CalculatePrice_ZeroWeight_ReturnsBasePrice()
    {
        // Arrange
        var rule = new ShippingPricingRule(
            "US-DOMESTIC",
            ShippingServiceLevel.Standard,
            1.50m,
            new Money(5.00m));

        // Act
        var price = rule.CalculatePrice(new Weight(0m));

        // Assert
        Assert.Equal(new Money(5.00m), price);
    }

    [Fact]
    public void CalculatePrice_NullWeight_ThrowsException()
    {
        // Arrange
        var rule = new ShippingPricingRule(
            "US-DOMESTIC",
            ShippingServiceLevel.Standard,
            1.50m,
            new Money(5.00m));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => rule.CalculatePrice(null!));
    }

    [Fact]
    public void DifferentServiceLevels_CreateDifferentRules()
    {
        // Arrange
        var standard = new ShippingPricingRule(
            "US-DOMESTIC", ShippingServiceLevel.Standard, 1.50m, new Money(5.00m));
        var express = new ShippingPricingRule(
            "US-DOMESTIC", ShippingServiceLevel.Express, 2.50m, new Money(10.00m));

        // Act
        var standardPrice = standard.CalculatePrice(new Weight(2.0m));
        var expressPrice = express.CalculatePrice(new Weight(2.0m));

        // Assert
        Assert.NotEqual(standardPrice, expressPrice);
    }

    [Fact]
    public void ReferenceEquality_SameInstance_ShouldBeEqual()
    {
        // Arrange
        var rule = new ShippingPricingRule("US-DOMESTIC", ShippingServiceLevel.Standard, 1.50m, new Money(5.00m));

        // Act & Assert
        Assert.Same(rule, rule);
    }

    [Fact]
    public void ReferenceEquality_DifferentInstances_ShouldNotBeEqual()
    {
        // Arrange
        var rule1 = new ShippingPricingRule("US-DOMESTIC", ShippingServiceLevel.Standard, 1.50m, new Money(5.00m));
        var rule2 = new ShippingPricingRule("US-DOMESTIC", ShippingServiceLevel.Standard, 1.50m, new Money(5.00m));

        // Act & Assert
        Assert.NotSame(rule1, rule2);
        Assert.NotEqual(rule1, rule2);
    }
}