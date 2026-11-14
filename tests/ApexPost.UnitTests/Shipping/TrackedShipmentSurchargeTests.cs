using ApexPost.Domain.Entities;
using Xunit;

namespace ApexPost.UnitTests.Shipping;

/// <summary>Cycle 3 of TDD-LOG.md: the surcharge rule is applied to a shipment, not just to a bare weight.</summary>
public sealed class TrackedShipmentSurchargeTests
{
    private static TrackedShipment ShipmentWeighing(string kg) => TrackedShipment.Create(
        "APX-20250101", decimal.Parse(kg, System.Globalization.CultureInfo.InvariantCulture),
        "Jane Example", "+15550100", "1 Example Street", "Springfield");

    [Theory]
    [InlineData("2.5", "0")]
    [InlineData("30", "0")]
    [InlineData("40", "20.00")]
    [InlineData("70", "42.50")]
    public void Surcharge_DependsOnTheShipmentWeight(string weight, string expected)
    {
        // Arrange
        var shipment = ShipmentWeighing(weight);

        // Act
        var surcharge = shipment.Surcharge;

        // Assert
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), surcharge);
    }

    [Fact]
    public void Surcharge_AfterWeightIsUpdatedOverThirtyKg_IsRecalculated()
    {
        // Arrange
        var shipment = ShipmentWeighing("5");

        // Act
        shipment.UpdateDetails(weightKg: 31m);

        // Assert
        Assert.Equal(13.25m, shipment.Surcharge);
    }
}
