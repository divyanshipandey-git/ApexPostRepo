
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public sealed class TransactionLineItemTests
{
    [Fact]
    public void Constructor_ValidValues_CreatesLineItem()
    {
        var item = new TransactionLineItem(
            "Envelope",
            "Retail",
            2,
            new Money(50m));

        Assert.Equal("Envelope", item.Description);
        Assert.Equal("Retail", item.ItemType);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(50m, item.UnitPrice.Amount);
    }

    [Fact]
    public void LineTotal_MultipleItems_ReturnsCorrectTotal()
    {
        var item = new TransactionLineItem(
            "Envelope",
            "Retail",
            2,
            new Money(50m));

        Assert.Equal(100m, item.LineTotal.Amount);
    }

    [Theory]
    [InlineData("Retail")]
    [InlineData("Service")]
    public void Constructor_ValidItemType_AcceptsValue(string itemType)
    {
        var item = new TransactionLineItem(
            "Counter item",
            itemType,
            1,
            new Money(25m));

        Assert.Equal(itemType, item.ItemType);
    }

    [Fact]
    public void Constructor_InvalidItemType_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() =>
            new TransactionLineItem(
                "Envelope",
                "Unknown",
                1,
                new Money(50m)));
    }

    [Fact]
    public void Constructor_ZeroQuantity_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TransactionLineItem(
                "Envelope",
                "Retail",
                0,
                new Money(50m)));
    }

    [Fact]
    public void Constructor_NegativeUnitPrice_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TransactionLineItem(
                "Envelope",
                "Retail",
                1,
                new Money(-10m)));
    }

    [Fact]
    public void Constructor_DescriptionWithWhitespace_TrimsValue()
    {
        var item = new TransactionLineItem(
            " Envelope ",
            "Retail",
            1,
            new Money(50m));

        Assert.Equal("Envelope", item.Description);
    }
}
