
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public sealed class MoneyOrderTests
{
    [Fact]
    public void Constructor_ValidValues_CreatesMoneyOrder()
    {
        var moneyOrder = new MoneyOrder(
            "MO001",
            new Money(1000m),
            "Amit Sharma",
            "Priya Verma",
            new Money(25m));

        Assert.Equal("MO001", moneyOrder.Id);
        Assert.Equal(1000m, moneyOrder.Amount.Amount);
        Assert.Equal("Amit Sharma", moneyOrder.SenderName);
        Assert.Equal("Priya Verma", moneyOrder.RecipientName);
        Assert.Equal(25m, moneyOrder.Fee.Amount);
    }

    [Fact]
    public void Constructor_NonPositiveAmount_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MoneyOrder(
                "MO001",
                new Money(0m),
                "Amit Sharma",
                "Priya Verma",
                new Money(25m)));
    }

    [Fact]
    public void Constructor_NegativeFee_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MoneyOrder(
                "MO001",
                new Money(1000m),
                "Amit Sharma",
                "Priya Verma",
                new Money(-5m)));
    }

    [Fact]
    public void TotalCost_ReturnsAmountPlusFee()
    {
        var moneyOrder = new MoneyOrder(
            "MO001",
            new Money(1000m),
            "Amit Sharma",
            "Priya Verma",
            new Money(25m));

        Assert.Equal(1025m, moneyOrder.TotalCost.Amount);
    }

    [Fact]
    public void Constructor_ValidNamesWithWhitespace_TrimsValues()
    {
        var moneyOrder = new MoneyOrder(
            " MO001 ",
            new Money(1000m),
            " Amit Sharma ",
            " Priya Verma ",
            new Money(25m));

        Assert.Equal("MO001", moneyOrder.Id);
        Assert.Equal("Amit Sharma", moneyOrder.SenderName);
        Assert.Equal("Priya Verma", moneyOrder.RecipientName);
    }
}
