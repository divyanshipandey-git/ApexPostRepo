using System;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

/// <summary>
/// Tests for Shipment entity
/// </summary>
public class ShipmentEntityTests
{
    [Fact]
    public void Constructor_ValidParcel_CreatesShipment()
    {
        // Act
        var shipment = new Shipment(
            "SHP-001",
            "Parcel",
            new Weight(2.5m),
            new Dimensions(10, 10, 10),
            "US-DOMESTIC",
            ShippingServiceLevel.Standard);

        // Assert
        Assert.Equal("SHP-001", shipment.Id);
        Assert.Equal("Parcel", shipment.ItemType);
        Assert.Equal(new Weight(2.5m), shipment.Weight);
        Assert.Equal("US-DOMESTIC", shipment.DestinationZone);
        Assert.Equal(ShippingServiceLevel.Standard, shipment.ServiceLevel);
    }

    [Fact]
    public void Constructor_ValidLetter_CreatesShipment()
    {
        // Act
        var shipment = new Shipment(
            "SHP-002",
            "Letter",
            new Weight(0.1m),
            new Dimensions(20, 10, 1),
            "US-DOMESTIC",
            ShippingServiceLevel.Express);

        // Assert
        Assert.Equal("Letter", shipment.ItemType);
        Assert.Equal(ShippingServiceLevel.Express, shipment.ServiceLevel);
    }

    [Fact]
    public void Constructor_InvalidItemType_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Shipment(
            "SHP-001", "Box", new Weight(1m), new Dimensions(10, 10, 10), "US-DOMESTIC", ShippingServiceLevel.Standard));
    }

    [Fact]
    public void Constructor_InvalidServiceLevel_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Shipment(
            "SHP-001", "Parcel", new Weight(1m), new Dimensions(10, 10, 10), "US-DOMESTIC", (ShippingServiceLevel)999));
    }

    [Fact]
    public void Constructor_WithBranchCode_SetsBranchCode()
    {
        // Act
        var shipment = new Shipment(
            "SHP-001",
            "Parcel",
            new Weight(1m),
            new Dimensions(10, 10, 10),
            "US-DOMESTIC",
            ShippingServiceLevel.Standard,
            branchCode: new BranchCode("BR001"));

        // Assert
        Assert.NotNull(shipment.BranchCode);
        Assert.Equal("BR001", shipment.BranchCode.Value);
    }

    [Fact]
    public void Constructor_WithCounterTransactionId_SetsTransactionId()
    {
        // Act
        var shipment = new Shipment(
            "SHP-001",
            "Parcel",
            new Weight(1m),
            new Dimensions(10, 10, 10),
            "US-DOMESTIC",
            ShippingServiceLevel.Standard,
            counterTransactionId: "TXN-001");

        // Assert
        Assert.Equal("TXN-001", shipment.CounterTransactionId);
    }

    [Fact]
    public void Constructor_InvalidCounterTransactionId_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Shipment(
            "SHP-001", "Parcel", new Weight(1m), new Dimensions(10, 10, 10),
            "US-DOMESTIC", ShippingServiceLevel.Standard,
            counterTransactionId: new string('a', 129)));
    }

    [Fact]
    public void Constructor_NonUtcCreatedAtUtc_ShouldReject()
    {
        // Act & Assert - passing a local time (offset not zero)
        var localTime = DateTimeOffset.Now;
        Assert.Throws<ArgumentException>(() => new Shipment(
            "SHP-001", "Parcel", new Weight(1m), new Dimensions(10, 10, 10),
            "US-DOMESTIC", ShippingServiceLevel.Standard,
            createdAtUtc: localTime));
    }

    [Fact]
    public void RecordCourierStatus_ValidStatus_RecordsStatus()
    {
        // Arrange
        var shipment = new Shipment(
            "SHP-001",
            "Parcel",
            new Weight(1m),
            new Dimensions(10, 10, 10),
            "US-DOMESTIC",
            ShippingServiceLevel.Standard);

        // Act
        shipment.RecordCourierStatus("Out for delivery", DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal("Out for delivery", shipment.CourierStatus);
        Assert.NotNull(shipment.CourierStatusUpdatedAtUtc);
    }

    [Fact]
    public void RecordCourierStatus_EmptyStatus_ShouldReject()
    {
        // Arrange
        var shipment = new Shipment(
            "SHP-001", "Parcel", new Weight(1m), new Dimensions(10, 10, 10),
            "US-DOMESTIC", ShippingServiceLevel.Standard);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => shipment.RecordCourierStatus("", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RecordCourierStatus_TooLongStatus_ShouldReject()
    {
        // Arrange
        var shipment = new Shipment(
            "SHP-001", "Parcel", new Weight(1m), new Dimensions(10, 10, 10),
            "US-DOMESTIC", ShippingServiceLevel.Standard);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => shipment.RecordCourierStatus(new string('a', 101), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RecordCourierStatus_NonUtcTimestamp_ShouldReject()
    {
        // Arrange
        var shipment = new Shipment(
            "SHP-001", "Parcel", new Weight(1m), new Dimensions(10, 10, 10),
            "US-DOMESTIC", ShippingServiceLevel.Standard);

        // Act & Assert - passing a local time (offset not zero)
        var localTime = DateTimeOffset.Now;
        Assert.Throws<ArgumentException>(() => shipment.RecordCourierStatus(
            "Delivered", localTime));
    }

    [Fact]
    public void ReferenceEquality_SameInstance_ShouldBeEqual()
    {
        // Arrange
        var shipment = new Shipment(
            "SHP-001", "Parcel", new Weight(1m), new Dimensions(10, 10, 10),
            "US-DOMESTIC", ShippingServiceLevel.Standard);

        // Act & Assert
        Assert.Same(shipment, shipment);
    }

    [Fact]
    public void ReferenceEquality_DifferentInstances_ShouldNotBeEqual()
    {
        // Arrange
        var shipment1 = new Shipment(
            "SHP-001", "Parcel", new Weight(1m), new Dimensions(10, 10, 10),
            "US-DOMESTIC", ShippingServiceLevel.Standard);
        var shipment2 = new Shipment(
            "SHP-001", "Parcel", new Weight(2m), new Dimensions(20, 20, 20),
            "US-DOMESTIC", ShippingServiceLevel.Standard);

        // Act & Assert
        Assert.NotSame(shipment1, shipment2);
        Assert.NotEqual(shipment1, shipment2);
    }
}