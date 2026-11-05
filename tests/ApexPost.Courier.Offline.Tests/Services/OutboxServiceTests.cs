using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Persistence;
using ApexPost.Courier.Offline.Services;

namespace ApexPost.Courier.Offline.Tests.Services;

public class OutboxServiceTests : IDisposable
{
    private readonly CourierDatabase _database;
    private readonly CouchbaseScanRepository _repository;
    private readonly OutboxService _outboxService;
    private readonly FakeShipmentUploader _uploader;

    public OutboxServiceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var dbPath = Path.Combine(Path.GetTempPath(), "ApexPost", "CourierOffline", $"Test_{Guid.NewGuid():N}");
        _database = new CourierDatabase(dbPath, loggerFactory.CreateLogger<CourierDatabase>());
        _repository = new CouchbaseScanRepository(_database);
        _uploader = new FakeShipmentUploader(0.0); // No failures
        _outboxService = new OutboxService(_repository, _uploader);
    }

    public void Dispose()
    {
        _database.RemoveChangeListener();
        _database.Dispose();
    }

    [Fact]
    public async Task ProcessPendingAsync_WithNoPendingScans_ReturnsZero()
    {
        var processed = await _outboxService.ProcessPendingAsync();

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessPendingAsync_WithPendingScans_MarksThemSynced()
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Warehouse A", DateTimeOffset.UtcNow);
        await _repository.CreateAsync(scan);

        var processed = await _outboxService.ProcessPendingAsync();

        processed.Should().Be(1);

        var updated = await _repository.GetByIdAsync(scan.Id);
        updated!.SyncStatus.Should().Be(SyncStatus.Synced);
    }

    [Fact]
    public async Task ProcessPendingAsync_WithMultiplePendingScans_ProcessesAll()
    {
        var scans = new[]
        {
            ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Location A", DateTimeOffset.UtcNow),
            ShipmentScan.Create("TRK-002", ScanType.InTransit, "COURIER-1", "Location B", DateTimeOffset.UtcNow),
            ShipmentScan.Create("TRK-003", ScanType.Delivered, "COURIER-1", "Location C", DateTimeOffset.UtcNow),
        };
        foreach (var scan in scans) await _repository.CreateAsync(scan);

        var processed = await _outboxService.ProcessPendingAsync();

        processed.Should().Be(3);

        foreach (var scan in scans)
        {
            var updated = await _repository.GetByIdAsync(scan.Id);
            updated!.SyncStatus.Should().Be(SyncStatus.Synced);
        }
    }

    [Fact]
    public async Task ProcessPendingAsync_WithUploaderFailure_MarksScanFailed()
    {
        var failUploader = new FakeShipmentUploader(1.0); // Always fails
        var outboxWithFailure = new OutboxService(_repository, failUploader);

        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Warehouse A", DateTimeOffset.UtcNow);
        await _repository.CreateAsync(scan);

        var processed = await outboxWithFailure.ProcessPendingAsync();

        processed.Should().Be(1);

        var updated = await _repository.GetByIdAsync(scan.Id);
        updated!.SyncStatus.Should().Be(SyncStatus.Failed);
    }

    [Fact]
    public async Task ProcessPendingAsync_AlreadySyncedScans_AreSkipped()
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Warehouse A", DateTimeOffset.UtcNow);
        scan.SyncStatus = SyncStatus.Synced;
        await _repository.CreateAsync(scan);

        var processed = await _outboxService.ProcessPendingAsync();

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessPendingAsync_AlreadyFailedScans_AreSkipped()
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Warehouse A", DateTimeOffset.UtcNow);
        scan.SyncStatus = SyncStatus.Failed;
        await _repository.CreateAsync(scan);

        var processed = await _outboxService.ProcessPendingAsync();

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessPendingAsync_MixedStatuses_OnlyProcessesPending()
    {
        var pendingScan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Location A", DateTimeOffset.UtcNow);
        var syncedScan = ShipmentScan.Create("TRK-002", ScanType.InTransit, "COURIER-1", "Location B", DateTimeOffset.UtcNow);
        syncedScan.SyncStatus = SyncStatus.Synced;
        var failedScan = ShipmentScan.Create("TRK-003", ScanType.Delivered, "COURIER-1", "Location C", DateTimeOffset.UtcNow);
        failedScan.SyncStatus = SyncStatus.Failed;

        await _repository.CreateAsync(pendingScan);
        await _repository.CreateAsync(syncedScan);
        await _repository.CreateAsync(failedScan);

        var processed = await _outboxService.ProcessPendingAsync();

        processed.Should().Be(1);

        var updatedPending = await _repository.GetByIdAsync(pendingScan.Id);
        updatedPending!.SyncStatus.Should().Be(SyncStatus.Synced);

        var updatedSynced = await _repository.GetByIdAsync(syncedScan.Id);
        updatedSynced!.SyncStatus.Should().Be(SyncStatus.Synced);

        var updatedFailed = await _repository.GetByIdAsync(failedScan.Id);
        updatedFailed!.SyncStatus.Should().Be(SyncStatus.Failed);
    }
}