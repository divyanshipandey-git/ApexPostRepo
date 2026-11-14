using ApexPost.Domain.Entities;
using Xunit;

namespace ApexPost.UnitTests.Shipping;

/// <summary>Boundary and format rules of TrackedShipment.Create (risk R7).</summary>
public sealed class TrackedShipmentCreationTests
{
    private static TrackedShipment Create(string tracking = "APX-20250101", decimal weight = 2.5m, string name = "Jane Example",
        string phone = "+15550100", string address = "1 Example Street", string city = "Springfield") =>
        TrackedShipment.Create(tracking, weight, name, phone, address, city);

    [Theory]
    [InlineData("APX-20250101")]
    [InlineData("APX-00000000")]
    [InlineData("APX-99999999")]
    public void Create_ValidTrackingNumber_IsAccepted(string tracking)
    {
        var shipment = Create(tracking: tracking);

        Assert.Equal(tracking, shipment.TrackingNumber);
        Assert.Equal(ShipmentStatus.Created, shipment.Status);
    }

    [Theory]
    [InlineData("APX-1234567")]      // 7 digits
    [InlineData("APX-123456789")]    // 9 digits
    [InlineData("apx-20250101")]     // lower case prefix
    [InlineData("APX20250101")]      // missing dash
    [InlineData("XPA-20250101")]     // wrong prefix
    [InlineData("APX-2025010A")]     // non digit
    [InlineData("APX-20250101\n")]   // trailing newline must not slip through $
    [InlineData("  APX-20250101  ")] // padding is invalid input, not silently trimmed
    public void Create_InvalidTrackingNumberFormat_ThrowsArgumentException(string tracking)
    {
        Assert.Throws<ArgumentException>(() => Create(tracking: tracking));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankTrackingNumber_ThrowsArgumentException(string? tracking)
    {
        Assert.ThrowsAny<ArgumentException>(() => Create(tracking: tracking!));
    }

    [Theory]
    [InlineData("0.1")]
    [InlineData("0.5")]
    [InlineData("70")]
    public void Create_WeightInsideRange_IsAccepted(string weight)
    {
        var shipment = Create(weight: decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture), shipment.WeightKg);
    }

    [Theory]
    [InlineData("0.09")]   // just below the minimum of 0.1 kg
    [InlineData("0.01")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("70.01")]  // just above the maximum of 70 kg
    [InlineData("500")]
    public void Create_WeightOutsideRange_ThrowsArgumentOutOfRange(string weight)
    {
        var value = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() => Create(weight: value));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("phone")]
    [InlineData("address")]
    [InlineData("city")]
    public void Create_BlankRequiredRecipientField_Throws(string field)
    {
        Assert.ThrowsAny<ArgumentException>(() => field switch
        {
            "name" => Create(name: " "),
            "phone" => Create(phone: ""),
            "address" => Create(address: " "),
            _ => Create(city: "")
        });
    }

    [Fact]
    public void Create_Always_StartsInCreatedStatusWithNoScans()
    {
        var shipment = Create();

        Assert.Equal(ShipmentStatus.Created, shipment.Status);
        Assert.Empty(shipment.Scans);
        Assert.False(string.IsNullOrWhiteSpace(shipment.Id));
    }

    [Fact]
    public void Create_ExplicitCreatedAt_IsUsedInsteadOfTheSystemClock()
    {
        var at = new DateTimeOffset(2024, 12, 31, 23, 59, 59, TimeSpan.Zero);

        var shipment = TrackedShipment.Create("APX-20250101", 1m, "Jane Example", "+15550100", "1 Example Street", "Springfield", createdAtUtc: at);

        Assert.Equal(at, shipment.CreatedAtUtc);
    }

    [Theory]
    [InlineData("0.09")]
    [InlineData("70.01")]
    [InlineData("0")]
    public void UpdateDetails_WeightOutsideRange_Throws(string weight)
    {
        var shipment = Create();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            shipment.UpdateDetails(weightKg: decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
