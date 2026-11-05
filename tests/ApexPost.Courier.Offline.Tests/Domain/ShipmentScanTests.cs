using System.Text.Json;
using Xunit;
using FluentAssertions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Persistence;
using ApexPost.Courier.Offline.Services;

namespace ApexPost.Courier.Offline.Tests.Domain;

public class ShipmentScanTests
{
    [Fact]
    public void Create_GeneratesValidIdWithScanPrefix()
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Warehouse A", DateTimeOffset.UtcNow);

        scan.Id.Should().StartWith("scan::");
        scan.Id.Should().MatchRegex(@"^scan::[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");
    }

    [Fact]
    public void Create_SetsAllPropertiesCorrectly()
    {
        var scannedAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var scan = ShipmentScan.Create("TRK-001", ScanType.Delivered, "COURIER-1", "Customer Site", scannedAt, "Signed by John");

        scan.TrackingNumber.Should().Be("TRK-001");
        scan.ScanType.Should().Be(ScanType.Delivered);
        scan.CourierId.Should().Be("COURIER-1");
        scan.Location.Should().Be("Customer Site");
        scan.ScannedAtUtc.Should().Be(scannedAt);
        scan.Notes.Should().Be("Signed by John");
        scan.SyncStatus.Should().Be(SyncStatus.Pending);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var scan = ShipmentScan.Create("  TRK-001  ", ScanType.Pickup, "  COURIER-1  ", "  Warehouse A  ", DateTimeOffset.UtcNow, "  notes  ");

        scan.TrackingNumber.Should().Be("TRK-001");
        scan.CourierId.Should().Be("COURIER-1");
        scan.Location.Should().Be("Warehouse A");
        scan.Notes.Should().Be("notes");
    }

    [Fact]
    public void Create_ThrowsOnNullTrackingNumber()
    {
        var act = () => ShipmentScan.Create("", ScanType.Pickup, "COURIER-1", "Location", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ThrowsOnNullCourierId()
    {
        var act = () => ShipmentScan.Create("TRK-001", ScanType.Pickup, "", "Location", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ThrowsOnNullLocation()
    {
        var act = () => ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SyncStatus_CanBeChanged()
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Location", DateTimeOffset.UtcNow);

        scan.SyncStatus = SyncStatus.Synced;
        scan.SyncStatus.Should().Be(SyncStatus.Synced);

        scan.SyncStatus = SyncStatus.Failed;
        scan.SyncStatus.Should().Be(SyncStatus.Failed);
    }

    [Fact]
    public void SerializesCorrectly()
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.InTransit, "COURIER-1", "Hub B", DateTimeOffset.UtcNow, "Transferred");

        var json = JsonSerializer.Serialize(scan);
        var deserialized = JsonSerializer.Deserialize<ShipmentScan>(json);

        deserialized.Should().NotBeNull();
        deserialized!.TrackingNumber.Should().Be("TRK-001");
        deserialized.ScanType.Should().Be(ScanType.InTransit);
        deserialized.CourierId.Should().Be("COURIER-1");
        deserialized.Location.Should().Be("Hub B");
        deserialized.Notes.Should().Be("Transferred");
        deserialized.SyncStatus.Should().Be(SyncStatus.Pending);
    }
}