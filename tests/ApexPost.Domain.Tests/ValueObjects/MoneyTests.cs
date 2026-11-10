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

    [Fact]
    public void Equality_DifferentInstancesSameAmount_ShouldBeEqual()
    {
        var m1 = new Money(25.50m);
        var m2 = new Money(25.50m);
        Assert.Equal(m1, m2);
        Assert.True(m1 == m2);
        Assert.False(m1 != m2);
    }

    [Fact]
    public void GetHashCode_SameAmount_ReturnsSameHash()
    {
        var m1 = new Money(25.50m);
        var m2 = new Money(25.50m);
        Assert.Equal(m1.GetHashCode(), m2.GetHashCode());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(10.0)]
    [InlineData(100.0)]
    [InlineData(1000.0)]
    [InlineData(10000.0)]
    public void Constructor_VariousValidAmounts_ShouldAccept(decimal amount)
    {
        var m = new Money(amount);
        Assert.Equal(Math.Round(amount, 2), m.Amount);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var money = new Money(123.45m);
        Assert.Contains("123.45", money.ToString());
    }

    [Fact]
    public void Zero_ReturnsZeroMoney()
    {
        var zero = Money.Zero;
        Assert.Equal(0m, zero.Amount);
    }

    [Fact]
    public void Add_NullOther_ThrowsException()
    {
        var money = new Money(100m);
        Assert.Throws<ArgumentNullException>(() => money.Add(null!));
    }

    [Fact]
    public void Subtract_NullOther_ThrowsException()
    {
        var money = new Money(100m);
        Assert.Throws<ArgumentNullException>(() => money.Subtract(null!));
    }

    [Fact]
    public void Currency_AlwaysReturnsINR()
    {
        var m1 = new Money(100m);
        var m2 = new Money(0m);
        var m3 = new Money(999999m);
        
        Assert.Equal("INR", m1.Currency);
        Assert.Equal("INR", m2.Currency);
        Assert.Equal("INR", m3.Currency);
    }
}