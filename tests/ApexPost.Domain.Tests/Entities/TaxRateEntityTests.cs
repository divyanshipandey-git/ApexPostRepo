using System;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

/// <summary>
/// Tests for TaxRate entity
/// </summary>
public class TaxRateEntityTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesTaxRate()
    {
        // Act
        var tr = new TaxRate(TaxCategory.Standard, 0.10m);

        // Assert
        Assert.Equal(TaxCategory.Standard, tr.Category);
        Assert.Equal(0.10m, tr.Rate);
    }

    [Fact]
    public void Constructor_ZeroRate_ShouldAccept()
    {
        // Act
        var tr = new TaxRate(TaxCategory.Standard, 0m);

        // Assert
        Assert.Equal(0m, tr.Rate);
    }

    [Fact]
    public void Constructor_NegativeRate_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaxRate(TaxCategory.Standard, -0.01m));
    }

    [Fact]
    public void Constructor_RateOverOne_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaxRate(TaxCategory.Standard, 1.5m));
    }

    [Fact]
    public void CalculateTax_ValidAmount_CalculatesCorrectly()
    {
        // Arrange
        var tr = new TaxRate(TaxCategory.Standard, 0.10m);

        // Act
        var tax = tr.CalculateTax(100.00m);

        // Assert
        Assert.Equal(10.00m, tax);
    }

    [Fact]
    public void CalculateTax_ZeroAmount_ReturnsZero()
    {
        // Arrange
        var tr = new TaxRate(TaxCategory.Standard, 0.10m);

        // Act
        var tax = tr.CalculateTax(0m);

        // Assert
        Assert.Equal(0m, tax);
    }

    [Fact]
    public void CalculateTax_WithReducedRate_CalculatesCorrectly()
    {
        // Arrange
        var tr = new TaxRate(TaxCategory.Reduced, 0.05m);

        // Act
        var tax = tr.CalculateTax(200.00m);

        // Assert
        Assert.Equal(10.00m, tax);
    }

    [Fact]
    public void CalculateTax_NegativeAmount_ShouldReject()
    {
        // Arrange
        var tr = new TaxRate(TaxCategory.Standard, 0.10m);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => tr.CalculateTax(-1m));
    }

    [Fact]
    public void ReferenceEquality_SameInstance_ShouldBeEqual()
    {
        // Arrange
        var tr = new TaxRate(TaxCategory.Standard, 0.10m);

        // Act & Assert
        Assert.Same(tr, tr);
    }

    [Fact]
    public void ReferenceEquality_DifferentInstances_ShouldNotBeEqual()
    {
        // Arrange
        var tr1 = new TaxRate(TaxCategory.Standard, 0.10m);
        var tr2 = new TaxRate(TaxCategory.Standard, 0.10m);

        // Act & Assert
        Assert.NotSame(tr1, tr2);
        Assert.NotEqual(tr1, tr2);
    }
}