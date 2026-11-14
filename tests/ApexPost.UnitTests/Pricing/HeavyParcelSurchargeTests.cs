using ApexPost.Domain.Pricing;
using Xunit;

namespace ApexPost.UnitTests.Pricing;

/// <summary>
/// Built test-first (see TDD-LOG.md). Rule: shipments over 30 kg require a surcharge.
/// </summary>
public sealed class HeavyParcelSurchargeTests
{
    [Theory]
    [InlineData("0.1")]
    [InlineData("10")]
    [InlineData("29.99")]
    [InlineData("30")]   // exactly 30 kg is NOT "over 30 kg"
    public void Calculate_AtOrBelowThirtyKg_IsZero(string weight)
    {
        // Arrange
        var kg = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var surcharge = HeavyParcelSurcharge.Calculate(kg);

        // Assert
        Assert.Equal(0m, surcharge);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("70.01")]
    public void Calculate_WeightOutsideAcceptedRange_Throws(string weight)
    {
        var kg = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() => HeavyParcelSurcharge.Calculate(kg));
    }

    [Theory]
    [InlineData("30.01", "12.51")]  // 12.50 + 0.01 kg * 0.75 = 12.5075, rounded to cents
    [InlineData("31", "13.25")]
    [InlineData("40", "20.00")]
    [InlineData("70", "42.50")]     // the heaviest accepted parcel
    public void Calculate_OverThirtyKg_IsFlatFeePlusRatePerKgAboveThirty(string weight, string expected)
    {
        // Arrange
        var kg = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);
        var expectedSurcharge = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var surcharge = HeavyParcelSurcharge.Calculate(kg);

        // Assert
        Assert.Equal(expectedSurcharge, surcharge);
    }

    [Fact]
    public void Calculate_JustOverThirtyKg_IsNeverCheaperThanTheFlatFee()
    {
        Assert.True(HeavyParcelSurcharge.Calculate(30.001m) >= 12.50m);
    }
}
