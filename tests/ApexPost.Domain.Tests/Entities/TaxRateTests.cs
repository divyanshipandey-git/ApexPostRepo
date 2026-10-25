
using ApexPost.Domain.Entities;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public sealed class TaxRateTests
{
    [Fact]
    public void Constructor_ValidValues_CreatesTaxRate()
    {
        var taxRate = new TaxRate(TaxCategory.Standard, 0.18m);

        Assert.Equal(TaxCategory.Standard, taxRate.Category);
        Assert.Equal(0.18m, taxRate.Rate);
    }

    [Fact]
    public void CalculateTax_ValidAmount_ReturnsCorrectTax()
    {
        var taxRate = new TaxRate(TaxCategory.Standard, 0.18m);

        var tax = taxRate.CalculateTax(100m);

        Assert.Equal(18m, tax);
    }

    [Fact]
    public void CalculateTax_ZeroRate_ReturnsZero()
    {
        var taxRate = new TaxRate(TaxCategory.ZeroRated, 0m);

        var tax = taxRate.CalculateTax(100m);

        Assert.Equal(0m, tax);
    }

    [Fact]
    public void CalculateTax_RoundsAwayFromZero()
    {
        var taxRate = new TaxRate(TaxCategory.Standard, 0.1m);

        var tax = taxRate.CalculateTax(10.05m);

        Assert.Equal(1.01m, tax);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Constructor_InvalidRate_ThrowsException(decimal rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TaxRate(TaxCategory.Standard, rate));
    }

    [Fact]
    public void CalculateTax_NegativeAmount_ThrowsException()
    {
        var taxRate = new TaxRate(TaxCategory.Standard, 0.18m);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => taxRate.CalculateTax(-100m));
    }

    [Fact]
    public void Constructor_InvalidCategory_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TaxRate((TaxCategory)999, 0.18m));
    }
}
