using ApexPost.Courier.Domain;
using ApexPost.Courier.Offline.Services;
using Moq;
using Xunit;

namespace ApexPost.UnitTests.Outbox;

// AI-ASSISTED: drafted with an AI assistant (another AI coding assistant), then reviewed and changed by a human; see COPILOT-LOG.md.
/// <summary>
/// OutboxService in isolation: IScanRepository and IShipmentUploader are Moq mocks.
/// What these tests do NOT prove: that the real Couchbase repository returns Pending/Failed scans
/// the way the mock does. That is covered by ApexPost.Courier.Offline.Tests against a real database.
/// </summary>
public sealed class OutboxServiceMoqTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IScanRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IShipmentUploader> _uploader = new(MockBehavior.Strict);

    private static ShipmentScan NewScan(string tracking = "APX-20250101", SyncStatus status = SyncStatus.Pending)
    {
        var scan = ShipmentScan.Create(tracking, ScanType.Pickup, "COURIER-001", "Depot A", Now);
        scan.SyncStatus = status;
        return scan;
    }

    private OutboxService CreateSut() => new(_repository.Object, _uploader.Object);

    private void GivenStored(IReadOnlyList<ShipmentScan> pending, IReadOnlyList<ShipmentScan> failed)
    {
        _repository.Setup(r => r.GetBySyncStatusAsync(SyncStatus.Pending, It.IsAny<CancellationToken>())).ReturnsAsync(pending);
        _repository.Setup(r => r.GetBySyncStatusAsync(SyncStatus.Failed, It.IsAny<CancellationToken>())).ReturnsAsync(failed);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShipmentScan s, CancellationToken _) => s);
    }

    [Fact]
    public async Task ProcessPendingAsync_UploadSucceeds_MarksScanSyncedAndSavesIt()
    {
        // Arrange
        var scan = NewScan();
        GivenStored([scan], []);
        _uploader.Setup(u => u.UploadAsync(scan, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var processed = await CreateSut().ProcessPendingAsync();

        // Assert
        Assert.Equal(1, processed);
        _repository.Verify(r => r.UpdateAsync(
            It.Is<ShipmentScan>(s => s.Id == scan.Id && s.SyncStatus == SyncStatus.Synced),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPendingAsync_UploadReturnsFalse_MarksScanFailed()
    {
        // Arrange
        var scan = NewScan();
        GivenStored([scan], []);
        _uploader.Setup(u => u.UploadAsync(scan, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        await CreateSut().ProcessPendingAsync();

        // Assert
        _repository.Verify(r => r.UpdateAsync(
            It.Is<ShipmentScan>(s => s.SyncStatus == SyncStatus.Failed), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPendingAsync_PreviouslyFailedScan_IsRetriedAfterPendingOnes()
    {
        // Arrange
        var pending = NewScan();
        var failed = NewScan(status: SyncStatus.Failed);
        GivenStored([pending], [failed]);
        var order = new List<string>();
        _uploader.Setup(u => u.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()))
            .Callback<ShipmentScan, CancellationToken>((s, _) => order.Add(s.Id))
            .ReturnsAsync(true);

        // Act
        var processed = await CreateSut().ProcessPendingAsync();

        // Assert
        Assert.Equal(2, processed);
        Assert.Equal([pending.Id, failed.Id], order);
    }

    [Fact]
    public async Task ProcessPendingAsync_UploaderThrows_MarksFailedAndContinuesWithNextScan()
    {
        // Arrange
        var first = NewScan();
        var second = NewScan();
        GivenStored([first, second], []);
        _uploader.Setup(u => u.UploadAsync(first, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("offline"));
        _uploader.Setup(u => u.UploadAsync(second, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var processed = await CreateSut().ProcessPendingAsync();

        // Assert
        Assert.Equal(2, processed);
        _repository.Verify(r => r.UpdateAsync(It.Is<ShipmentScan>(s => s.Id == first.Id && s.SyncStatus == SyncStatus.Failed), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.UpdateAsync(It.Is<ShipmentScan>(s => s.Id == second.Id && s.SyncStatus == SyncStatus.Synced), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPendingAsync_NothingToSync_NeverCallsUploaderOrUpdate()
    {
        // Arrange
        GivenStored([], []);

        // Act
        var processed = await CreateSut().ProcessPendingAsync();

        // Assert
        Assert.Equal(0, processed);
        _uploader.Verify(u => u.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPendingAsync_CancelledToken_ThrowsAndDoesNotUploadOrSave()
    {
        // Arrange
        GivenStored([NewScan()], []);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act + Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().ProcessPendingAsync(cts.Token));
        _uploader.Verify(u => u.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPendingAsync_UploaderCancelled_PropagatesCancellationInsteadOfMarkingFailed()
    {
        // Arrange
        var scan = NewScan();
        GivenStored([scan], []);
        _uploader.Setup(u => u.UploadAsync(scan, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        // Act + Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateSut().ProcessPendingAsync());
        _repository.Verify(r => r.UpdateAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new OutboxService(null!, _uploader.Object));
        Assert.Throws<ArgumentNullException>(() => new OutboxService(_repository.Object, null!));
    }
}
