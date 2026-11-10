using Xunit;
using FluentAssertions;
using Moq;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Services;

namespace ApexPost.Courier.Offline.Tests.Services;

public class OutboxServiceTests
{
    private static ShipmentScan NewScan(SyncStatus status)
    {
        var scan = ShipmentScan.Create("TRK-001", ScanType.Pickup, "COURIER-1", "Warehouse A", DateTimeOffset.UtcNow);
        scan.SyncStatus = status;
        return scan;
    }

    [Fact]
    public async Task ProcessPendingAsync_RetriesFailedScans_AndMarksThemSynced()
    {
        var failed = NewScan(SyncStatus.Failed);
        var repository = new Mock<IScanRepository>();
        repository.Setup(r => r.GetBySyncStatusAsync(SyncStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ShipmentScan>());
        repository.Setup(r => r.GetBySyncStatusAsync(SyncStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { failed });
        repository.Setup(r => r.UpdateAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShipmentScan s, CancellationToken _) => s);

        var uploader = new Mock<IShipmentUploader>();
        uploader.Setup(u => u.UploadAsync(failed, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var service = new OutboxService(repository.Object, uploader.Object);

        var processed = await service.ProcessPendingAsync();

        processed.Should().Be(1);
        failed.SyncStatus.Should().Be(SyncStatus.Synced);
        repository.Verify(r => r.UpdateAsync(failed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenUploadThrows_MarksScanFailedSoItIsRetriedLater()
    {
        var pending = NewScan(SyncStatus.Pending);
        var repository = new Mock<IScanRepository>();
        repository.Setup(r => r.GetBySyncStatusAsync(SyncStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pending });
        repository.Setup(r => r.GetBySyncStatusAsync(SyncStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ShipmentScan>());
        repository.Setup(r => r.UpdateAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShipmentScan s, CancellationToken _) => s);

        var uploader = new Mock<IShipmentUploader>();
        uploader.Setup(u => u.UploadAsync(pending, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));

        var service = new OutboxService(repository.Object, uploader.Object);

        await service.ProcessPendingAsync();

        pending.SyncStatus.Should().Be(SyncStatus.Failed);
    }
}
