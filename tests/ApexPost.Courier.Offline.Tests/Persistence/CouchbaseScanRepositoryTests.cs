using System.Text.Json;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Persistence;
using ApexPost.Courier.Offline.Services;

namespace ApexPost.Courier.Offline.Tests.Persistence;

public class CouchbaseScanRepositoryTests : IDisposable
{
    private readonly CourierDatabase _database;
    private readonly CouchbaseScanRepository _repository;

    public CouchbaseScanRepositoryTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var dbPath = Path.Combine(Path.GetTempPath(), "ApexPost", "CourierOffline", $"Test_{Guid.NewGuid():N}");
        _database = new CourierDatabase(dbPath, loggerFactory.CreateLogger<CourierDatabase>());
        _repository = new CouchbaseScanRepository(_database);
    }

    public void Dispose()
    {
        _database.RemoveChangeListener();
        _database.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ThenGetByIdAsync_ReturnsSameScan()
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Warehouse A", DateTimeOffset.UtcNow);

        await _repository.CreateAsync(scan);

        var retrieved = await _repository.GetByIdAsync(scan.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(scan.Id);
        retrieved.TrackingNumber.Should().Be(scan.TrackingNumber);
        retrieved.ScanType.Should().Be(scan.ScanType);
        retrieved.CourierId.Should().Be(scan.CourierId);
        retrieved.Location.Should().Be(scan.Location);
        retrieved.SyncStatus.Should().Be(SyncStatus.Pending);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync("scan::non-existent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ModifiesExistingScan()
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Warehouse A", DateTimeOffset.UtcNow);
        await _repository.CreateAsync(scan);

        // Create updated scan with same ID but different properties
        var updatedScan = new ShipmentScan
        {
            Id = scan.Id,
            TrackingNumber = scan.TrackingNumber,
            ScanType = scan.ScanType,
            CourierId = scan.CourierId,
            Location = scan.Location,
            ScannedAtUtc = scan.ScannedAtUtc,
            Notes = "Updated note",
            SyncStatus = SyncStatus.Synced
        };
        await _repository.UpdateAsync(updatedScan);

        var retrieved = await _repository.GetByIdAsync(scan.Id);

        retrieved!.SyncStatus.Should().Be(SyncStatus.Synced);
        retrieved.Notes.Should().Be("Updated note");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentId_Throws()
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Warehouse A", DateTimeOffset.UtcNow);

        var act = async () => await _repository.UpdateAsync(scan);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteAsync_RemovesScan()
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Warehouse A", DateTimeOffset.UtcNow);
        await _repository.CreateAsync(scan);

        await _repository.DeleteAsync(scan.Id);

        var retrieved = await _repository.GetByIdAsync(scan.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        var act = async () => await _repository.DeleteAsync("scan::non-existent");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListAllAsync_ReturnsAllScans()
    {
        var scan1 = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Location A", DateTimeOffset.UtcNow);
        var scan2 = ShipmentScan.Create("TRK-002", ScanType.Delivered, "COURIER-2", "Location B", DateTimeOffset.UtcNow);
        await _repository.CreateAsync(scan1);
        await _repository.CreateAsync(scan2);

        var all = await _repository.ListAllAsync();

        all.Should().HaveCount(2);
        all.Should().Contain(s => s.Id == scan1.Id);
        all.Should().Contain(s => s.Id == scan2.Id);
    }

    [Fact]
    public async Task GetByTrackingNumberAsync_ReturnsScansOrderedByTime()
    {
        var now = DateTimeOffset.UtcNow;
        var scan1 = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Location A", now.AddMinutes(-10));
        var scan2 = ShipmentScan.Create("TRK-001", ScanType.InTransit, "COURIER-1", "Location B", now.AddMinutes(-5));
        var scan3 = ShipmentScan.Create("TRK-001", ScanType.Delivered, "COURIER-1", "Location C", now);
        await _repository.CreateAsync(scan1);
        await _repository.CreateAsync(scan2);
        await _repository.CreateAsync(scan3);

        var results = await _repository.GetByTrackingNumberAsync("TRK-001");

        results.Should().HaveCount(3);
        results[0].ScanType.Should().Be(ScanType.Pickup);
        results[1].ScanType.Should().Be(ScanType.InTransit);
        results[2].ScanType.Should().Be(ScanType.Delivered);
    }

    [Fact]
    public async Task GetByTrackingNumberAsync_WithDifferentTrackingNumber_ReturnsEmpty()
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Location A", DateTimeOffset.UtcNow);
        await _repository.CreateAsync(scan);

        var results = await _repository.GetByTrackingNumberAsync("TRK-999");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBySyncStatusAsync_ReturnsMatchingScans()
    {
        var pendingScan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Location A", DateTimeOffset.UtcNow);
        var syncedScan = ShipmentScan.Create("TRK-002", ScanType.Delivered, "COURIER-1", "Location B", DateTimeOffset.UtcNow);
        syncedScan.SyncStatus = SyncStatus.Synced;
        await _repository.CreateAsync(pendingScan);
        await _repository.CreateAsync(syncedScan);

        var pending = await _repository.GetBySyncStatusAsync(SyncStatus.Pending);
        var synced = await _repository.GetBySyncStatusAsync(SyncStatus.Synced);
        var failed = await _repository.GetBySyncStatusAsync(SyncStatus.Failed);

        pending.Should().HaveCount(1).And.Contain(s => s.Id == pendingScan.Id);
        synced.Should().HaveCount(1).And.Contain(s => s.Id == syncedScan.Id);
        failed.Should().BeEmpty();
    }

    [Fact]
    public async Task GetScanCountsByTypeForCourierAsync_ReturnsCorrectCounts()
    {
        var courierId = "COURIER-1";
        var scans = new[]
        {
            ShipmentScan.Create("TRK-001", ScanType.Pickup, courierId, "Location A", DateTimeOffset.UtcNow),
            ShipmentScan.Create("TRK-002", ScanType.Pickup, courierId, "Location B", DateTimeOffset.UtcNow),
            ShipmentScan.Create("TRK-003", ScanType.InTransit, courierId, "Location C", DateTimeOffset.UtcNow),
            ShipmentScan.Create("TRK-004", ScanType.Delivered, courierId, "Location D", DateTimeOffset.UtcNow),
            ShipmentScan.Create("TRK-005", ScanType.Delivered, courierId, "Location E", DateTimeOffset.UtcNow),
            ShipmentScan.Create("TRK-006", ScanType.Delivered, courierId, "Location F", DateTimeOffset.UtcNow),
        };
        foreach (var scan in scans) await _repository.CreateAsync(scan);

        var counts = await _repository.GetScanCountsByTypeForCourierAsync(courierId);

        counts.Should().HaveCount(3);
        counts[ScanType.Pickup].Should().Be(2);
        counts[ScanType.InTransit].Should().Be(1);
        counts[ScanType.Delivered].Should().Be(3);
        counts.Should().NotContainKey(ScanType.Failed);
    }

    [Fact]
    public async Task GetScanCountsByTypeForCourierAsync_WithDifferentCourier_ReturnsEmpty()
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Location A", DateTimeOffset.UtcNow);
        await _repository.CreateAsync(scan);

        var counts = await _repository.GetScanCountsByTypeForCourierAsync("COURIER-999");

        counts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetScanCountsByTypeForCourierAsync_ThrowsOnNullCourierId()
    {
        var act = async () => await _repository.GetScanCountsByTypeForCourierAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}