
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public class ShipmentTests
{
    [Fact]
    public void Constructor_ShouldCreateValidParcel()
    {
        var shipment = CreateShipment();

        Assert.Equal("SH001", shipment.Id);
        Assert.Equal("Parcel", shipment.ItemType);
        Assert.Equal(2.5m, shipment.Weight.Kilograms);
        Assert.Equal("ZONE1", shipment.DestinationZone);
        Assert.Equal(
            ShippingServiceLevel.Standard,
            shipment.ServiceLevel);
    }

    [Fact]
    public void Constructor_ShouldRejectUnsupportedItemType()
    {
        Assert.Throws<ArgumentException>(() =>
            new Shipment(
                "SH001",
                "Document",
                new Weight(1m),
                10m, 10m, 10m,
                "ZONE1",
                ShippingServiceLevel.Standard));
    }

    [Fact]
    public void Constructor_ShouldRejectNonPositiveDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Shipment(
                "SH001",
                "Parcel",
                new Weight(1m),
                0m, 10m, 10m,
                "ZONE1",
                ShippingServiceLevel.Standard));
    }

    [Fact]
    public void Constructor_ShouldRejectUndefinedServiceLevel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Shipment(
                "SH001",
                "Parcel",
                new Weight(1m),
                10m, 10m, 10m,
                "ZONE1",
                (ShippingServiceLevel)999));
    }

    [Fact]
    public void Constructor_ShouldTrimIdAndDestinationZone()
    {
        var shipment = new Shipment(
            " SH001 ",
            "Parcel",
            new Weight(1m),
            10m, 10m, 10m,
            " ZONE1 ",
            ShippingServiceLevel.Express);

        Assert.Equal("SH001", shipment.Id);
        Assert.Equal("ZONE1", shipment.DestinationZone);
    }

    private static Shipment CreateShipment() =>
        new(
            "SH001",
            "Parcel",
            new Weight(2.5m),
            30m, 20m, 15m,
            "ZONE1",
            ShippingServiceLevel.Standard);
}
