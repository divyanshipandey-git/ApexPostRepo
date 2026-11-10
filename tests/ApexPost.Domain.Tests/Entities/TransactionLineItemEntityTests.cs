using System;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

/// <summary>
/// Tests for TransactionLineItem entity
/// </summary>
public class TransactionLineItemEntityTests
{
    [Fact]
    public void Constructor_ValidRetailItem_CreatesLineItem()
    {
        // Act
        var item = new TransactionLineItem(
            "Product 1",
            "Retail",
            2,
            new Money(10.00m),
            TaxCategory.Standard);

        // Assert
        Assert.Equal("Product 1", item.Description);
        Assert.Equal("Retail", item.ItemType);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(new Money(10.00m), item.UnitPrice);
        Assert.Equal(TaxCategory.Standard, item.TaxCategory);
        Assert.Equal(new Money(20.00m), item.LineTotal);
    }

    [Fact]
    public void Constructor_ValidServiceItem_CreatesLineItem()
    {
        // Act
        var item = new TransactionLineItem(
            "Shipping Service",
            "Service",
            1,
            new Money(15.00m),
            TaxCategory.Reduced);

        // Assert
        Assert.Equal("Service", item.ItemType);
        Assert.Equal(TaxCategory.Reduced, item.TaxCategory);
    }

    [Fact]
    public void Constructor_EmptyDescription_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TransactionLineItem(
            "", "Retail", 1, new Money(10.00m), TaxCategory.Standard));
    }

    [Fact]
    public void Constructor_InvalidItemType_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TransactionLineItem(
            "Product", "Product", 1, new Money(10.00m), TaxCategory.Standard));
    }

    [Fact]
    public void Constructor_ZeroQuantity_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransactionLineItem(
            "Product", "Retail", 0, new Money(10.00m), TaxCategory.Standard));
    }

    [Fact]
    public void Constructor_NegativeQuantity_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransactionLineItem(
            "Product", "Retail", -1, new Money(10.00m), TaxCategory.Standard));
    }

    [Fact]
    public void Constructor_NegativeUnitPrice_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransactionLineItem(
            "Product", "Retail", 1, new Money(-10.00m), TaxCategory.Standard));
    }

    [Fact]
    public void Constructor_InvalidTaxCategory_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransactionLineItem(
            "Product", "Retail", 1, new Money(10.00m), (TaxCategory)999));
    }

    [Fact]
    public void Constructor_ReferenceIdTooLong_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TransactionLineItem(
            "Product", "Retail", 1, new Money(10.00m), TaxCategory.Standard,
            referenceId: new string('a', 129)));
    }

    [Fact]
    public void Constructor_ReferenceIdWithControlChars_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TransactionLineItem(
            "Product", "Retail", 1, new Money(10.00m), TaxCategory.Standard,
            referenceId: "REF\t123"));
    }

    [Fact]
    public void Constructor_WithReferenceId_SetsReferenceId()
    {
        // Act
        var item = new TransactionLineItem(
            "Product", "Retail", 1, new Money(10.00m), TaxCategory.Standard,
            referenceId: "REF-123");

        // Assert
        Assert.Equal("REF-123", item.ReferenceId);
    }

    [Fact]
    public void LineTotal_CalculatesCorrectly()
    {
        // Arrange
        var item = new TransactionLineItem(
            "Product", "Retail", 3, new Money(10.50m), TaxCategory.Standard);

        // Act
        var total = item.LineTotal;

        // Assert
        Assert.Equal(new Money(31.50m), total);
    }

    [Fact]
    public void ReferenceEquality_SameInstance_ShouldBeEqual()
    {
        // Arrange
        var item = new TransactionLineItem(
            "Product", "Retail", 1, new Money(10.00m), TaxCategory.Standard);

        // Act & Assert
        Assert.Same(item, item);
    }

    [Fact]
    public void ReferenceEquality_DifferentInstances_ShouldNotBeEqual()
    {
        // Arrange
        var item1 = new TransactionLineItem(
            "Product", "Retail", 1, new Money(10.00m), TaxCategory.Standard);
        var item2 = new TransactionLineItem(
            "Product", "Retail", 1, new Money(10.00m), TaxCategory.Standard);

        // Act & Assert
        Assert.NotSame(item1, item2);
        Assert.NotEqual(item1, item2);
    }
}