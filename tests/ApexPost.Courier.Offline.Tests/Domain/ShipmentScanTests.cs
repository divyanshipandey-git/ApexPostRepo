using System;
using ApexPost.Courier.Offline.Domain;
using Xunit;

namespace ApexPost.Courier.Offline.Tests.Domain;

/// <summary>
/// Tests for ShipmentScan domain entity
/// </summary>
public class ShipmentScanTests
{
    [Fact]
    public void Create_ValidParameters_CreatesScan()
    {
        // Act
        var scan = ShipmentScan.Create(
            trackingNumber: "APX-20240101",
            scanType: ScanType.Pickup,
            courierId: "COURIER-001",
            location: "Warehouse A",
            scannedAtUtc: DateTimeOffset.UtcNow,
            notes: "Test scan");

        // Assert
        Assert.NotNull(scan);
        Assert.StartsWith("scan::", scan.Id);
        Assert.Equal("APX-20240101", scan.TrackingNumber);
        Assert.Equal(ScanType.Pickup, scan.ScanType);
        Assert.Equal("COURIER-001", scan.CourierId);
        Assert.Equal("Warehouse A", scan.Location);
        Assert.Equal("Test scan", scan.Notes);
        Assert.Equal(SyncStatus.Pending, scan.SyncStatus);
    }

    [Fact]
    public void Create_EmptyTrackingNumber_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ShipmentScan.Create(
            trackingNumber: "",
            scanType: ScanType.Pickup,
            courierId: "COURIER-001",
            location: "Warehouse",
            scannedAtUtc: DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_EmptyCourierId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ShipmentScan.Create(
            trackingNumber: "APX-20240101",
            scanType: ScanType.Pickup,
            courierId: "",
            location: "Warehouse",
            scannedAtUtc: DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_EmptyLocation_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ShipmentScan.Create(
            trackingNumber: "APX-20240101",
            scanType: ScanType.Pickup,
            courierId: "COURIER-001",
            location: "",
            scannedAtUtc: DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_WithNotes_StoresNotes()
    {
        // Act
        var scan = ShipmentScan.Create(
            trackingNumber: "APX-20240101",
            scanType: ScanType.Pickup,
            courierId: "COURIER-001",
            location: "Warehouse",
            scannedAtUtc: DateTimeOffset.UtcNow,
            notes: "  Test notes with spaces  ");

        // Assert
        Assert.Equal("Test notes with spaces", scan.Notes);
    }

    [Fact]
    public void Create_NullNotes_StoresNull()
    {
        // Act
        var scan = ShipmentScan.Create(
            trackingNumber: "APX-20240101",
            scanType: ScanType.Pickup,
            courierId: "COURIER-001",
            location: "Warehouse",
            scannedAtUtc: DateTimeOffset.UtcNow,
            notes: null);

        // Assert
        Assert.Null(scan.Notes);
    }

    [Fact]
    public void SyncStatus_CanBeChanged()
    {
        // Arrange
        var scan = ShipmentScan.Create(
            trackingNumber: "APX-20240101",
            scanType: ScanType.Pickup,
            courierId: "COURIER-001",
            location: "Warehouse",
            scannedAtUtc: DateTimeOffset.UtcNow);

        // Act
        scan.SyncStatus = SyncStatus.Synced;

        // Assert
        Assert.Equal(SyncStatus.Synced, scan.SyncStatus);
    }

    [Fact]
    public void ReferenceEquality_SameInstance_ShouldBeEqual()
    {
        // Arrange
        var scan = ShipmentScan.Create(
            trackingNumber: "APX-20240101",
            scanType: ScanType.Pickup,
            courierId: "COURIER-001",
            location: "Warehouse",
            scannedAtUtc: DateTimeOffset.UtcNow);
        var sameReference = scan;

        // Act & Assert
        Assert.Same(scan, sameReference);
        Assert.True(scan == sameReference);
    }

    [Fact]
    public void ReferenceEquality_DifferentInstances_ShouldNotBeEqual()
    {
        // Arrange
        var scan1 = ShipmentScan.Create("APX-1", ScanType.Pickup, "COURIER-001", "A", DateTimeOffset.UtcNow);
        var scan2 = ShipmentScan.Create("APX-1", ScanType.Pickup, "COURIER-001", "A", DateTimeOffset.UtcNow);

        // Act & Assert - Different instances are not equal by reference
        Assert.NotSame(scan1, scan2);
        Assert.False(scan1 == scan2);
    }

    [Theory]
    [InlineData(ScanType.Pickup)]
    [InlineData(ScanType.InTransit)]
    [InlineData(ScanType.Delivered)]
    [InlineData(ScanType.Failed)]
    public void Create_AllScanTypes_ShouldWork(ScanType scanType)
    {
        // Act
        var scan = ShipmentScan.Create(
            trackingNumber: "APX-20240101",
            scanType: scanType,
            courierId: "COURIER-001",
            location: "Test Location",
            scannedAtUtc: DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(scanType, scan.ScanType);
    }
}