
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_ShouldRoundToTwoDecimalPlaces()
    {
        var money = new Money(10.456m);

        Assert.Equal(10.46m, money.Amount);
    }

    [Fact]
    public void Add_ShouldReturnSumOfTwoAmounts()
    {
        var first = new Money(100.25m);
        var second = new Money(50.50m);

        var result = first.Add(second);

        Assert.Equal(150.75m, result.Amount);
    }

    [Fact]
    public void Subtract_ShouldReturnDifferenceOfTwoAmounts()
    {
        var first = new Money(100m);
        var second = new Money(30m);

        var result = first.Subtract(second);

        Assert.Equal(70m, result.Amount);
    }

    [Fact]
    public void Currency_ShouldBeINR()
    {
        var money = new Money(100m);

        Assert.Equal("INR", money.Currency);
    }
}
