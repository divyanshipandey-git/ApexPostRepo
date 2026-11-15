// COPILOT-ASSISTED: drafted with GitHub Copilot (prompt 4 in COPILOT-LOG.md), then reviewed and extended by the student.
using System.Net;
using ApexPost.Courier.Console.Models;
using ApexPost.Courier.Console.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ApexPost.UnitTests.Client;

public sealed class SyncServiceCopilotTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static LocalScanDocument NewScan(string scanId, string trackingNumber = "APX-20250101", int index = 0) => new()
    {
        ScanId = scanId,
        TrackingNumber = trackingNumber,
        ScanType = ScanType.Pickup,
        CourierId = "COURIER-001",
        Location = "Depot A",
        ScannedAtUtc = Now.AddMinutes(index),
        SyncStatus = SyncStatus.Pending
    };

    private static SyncService CreateSut(
        Mock<IApexApiClient>? apiClient = null,
        Mock<ICourierDatabase>? database = null,
        Mock<ITokenStorage>? tokenStorage = null,
        Mock<IResilienceStateTracker>? resilience = null,
        SyncOptions? options = null)
    {
        apiClient ??= new Mock<IApexApiClient>();
        database ??= new Mock<ICourierDatabase>();
        tokenStorage ??= new Mock<ITokenStorage>();
        resilience ??= new Mock<IResilienceStateTracker>();
        resilience.SetupGet(r => r.IsCircuitOpen).Returns(false);

        database.Setup(d => d.MarkScanSyncedAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        database.Setup(d => d.MarkScanFailedAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        tokenStorage.Setup(t => t.ClearTokenAsync()).Returns(Task.CompletedTask);

        return new SyncService(
            apiClient.Object,
            database.Object,
            resilience.Object,
            tokenStorage.Object,
            Options.Create(options ?? new SyncOptions { BatchSize = 20 }),
            NullLogger<SyncService>.Instance);
    }

    [Fact]
    public async Task SyncPendingScansAsync_FortyFivePendingScansInThreeBatches_UsesThreeUploadsAndMarksAllSynced()
    {
        // Arrange
        var pending = Enumerable.Range(0, 45)
            .Select(i => NewScan($"scan::{i:D4}", "APX-20250101", i))
            .ToList();

        var apiClient = new Mock<IApexApiClient>();
        apiClient.SetupSequence(a => a.UploadScansAsync(
                It.IsAny<string>(),
                It.IsAny<ScanUploadRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanUploadResultDto { Accepted = 20, Rejected = 0, DuplicatesSkipped = 0, RejectionDetails = [] })
            .ReturnsAsync(new ScanUploadResultDto { Accepted = 20, Rejected = 0, DuplicatesSkipped = 0, RejectionDetails = [] })
            .ReturnsAsync(new ScanUploadResultDto { Accepted = 5, Rejected = 0, DuplicatesSkipped = 0, RejectionDetails = [] });

        var database = new Mock<ICourierDatabase>();
        database.Setup(d => d.GetPendingScansAsync()).ReturnsAsync(pending);
        database.Setup(d => d.MarkScanSyncedAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var sut = CreateSut(apiClient, database);

        // Act
        var result = await sut.SyncPendingScansAsync();

        // Assert
        Assert.Equal(45, result.ScansSynced);
        Assert.Equal(0, result.ScansFailed);
        Assert.Equal(0, result.ScansDeferred);
        apiClient.Verify(a => a.UploadScansAsync(
            "APX-20250101",
            It.Is<ScanUploadRequestDto>(r => r.Scans.Count == 20),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        apiClient.Verify(a => a.UploadScansAsync(
            "APX-20250101",
            It.Is<ScanUploadRequestDto>(r => r.Scans.Count == 5),
            It.IsAny<CancellationToken>()), Times.Once);
        database.Verify(d => d.MarkScanSyncedAsync(It.IsAny<string>()), Times.Exactly(45));
    }

    [Fact]
    public async Task SyncPendingScansAsync_BatchWithRejections_MarksOnlyFailedOnesAndCountsFailures()
    {
        // Arrange
        var pending = new[]
        {
            NewScan("scan::001"),
            NewScan("scan::002")
        };

        var apiClient = new Mock<IApexApiClient>();
        apiClient.Setup(a => a.UploadScansAsync(
                "APX-20250101",
                It.IsAny<ScanUploadRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanUploadResultDto
            {
                Accepted = 1,
                Rejected = 1,
                DuplicatesSkipped = 0,
                RejectionDetails =
                [
                    new ScanRejectionDto { ScanId = "scan::002", Reason = "Bad scan" }
                ]
            });

        var database = new Mock<ICourierDatabase>();
        database.Setup(d => d.GetPendingScansAsync()).ReturnsAsync(pending);

        var sut = CreateSut(apiClient, database);

        // Act
        var result = await sut.SyncPendingScansAsync();

        // Assert
        Assert.Equal(1, result.ScansSynced);
        Assert.Equal(1, result.ScansFailed);
        database.Verify(d => d.MarkScanSyncedAsync("scan::001"), Times.Once);
        database.Verify(d => d.MarkScanFailedAsync("scan::002", "Bad scan"), Times.Once);
    }

    [Fact]
    public async Task SyncPendingScansAsync_Conflict409_DefersBatchAndDoesNotMarkScans()
    {
        // Arrange
        var pending = Enumerable.Range(0, 3)
            .Select(i => NewScan($"scan::{i:D4}", "APX-20250101", i))
            .ToList();

        var apiClient = new Mock<IApexApiClient>();
        apiClient.Setup(a => a.UploadScansAsync(
                "APX-20250101",
                It.IsAny<ScanUploadRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Conflict", 409, "Upload is in progress", "https://example.com/conflict"));

        var database = new Mock<ICourierDatabase>();
        database.Setup(d => d.GetPendingScansAsync()).ReturnsAsync(pending);

        var sut = CreateSut(apiClient, database);

        // Act
        var result = await sut.SyncPendingScansAsync();

        // Assert
        Assert.Equal(3, result.ScansDeferred);
        Assert.Equal(0, result.ScansSynced);
        Assert.Equal(0, result.ScansFailed);
        database.Verify(d => d.MarkScanSyncedAsync(It.IsAny<string>()), Times.Never);
        database.Verify(d => d.MarkScanFailedAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SyncPendingScansAsync_Unauthorized401_ClearsTokenAndReturnsError()
    {
        // Arrange
        var pending = new[] { NewScan("scan::001") };

        var apiClient = new Mock<IApexApiClient>();
        apiClient.Setup(a => a.UploadScansAsync(
                "APX-20250101",
                It.IsAny<ScanUploadRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Unauthorized", 401, "Please log in again", "https://example.com/unauthorized"));

        var database = new Mock<ICourierDatabase>();
        database.Setup(d => d.GetPendingScansAsync()).ReturnsAsync(pending);

        var tokenStorage = new Mock<ITokenStorage>();
        tokenStorage.Setup(t => t.ClearTokenAsync()).Returns(Task.CompletedTask);

        var sut = CreateSut(apiClient, database, tokenStorage);

        // Act
        var result = await sut.SyncPendingScansAsync();

        // Assert
        Assert.Equal(1, result.ScansFailed);
        Assert.Equal("Authentication failed - please log in again", result.ErrorMessage);
        Assert.Equal(SyncState.Error, sut.CurrentStatus);
        tokenStorage.Verify(t => t.ClearTokenAsync(), Times.Once);
    }

    // Added in review: Copilot did not draft the 500 case or the empty queue that the prompt asked for.
    [Fact]
    public async Task SyncPendingScansAsync_ServerError500_CountsScansAsFailedAndNeverMarksThemSynced()
    {
        // Arrange
        var pending = new[] { NewScan("scan::001"), NewScan("scan::002") };
        var apiClient = new Mock<IApexApiClient>();
        apiClient.Setup(a => a.UploadScansAsync("APX-20250101", It.IsAny<ScanUploadRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Server error", 500, "boom", null));
        var database = new Mock<ICourierDatabase>();
        database.Setup(d => d.GetPendingScansAsync()).ReturnsAsync(pending);
        var sut = CreateSut(apiClient, database);

        // Act
        var result = await sut.SyncPendingScansAsync();

        // Assert
        Assert.Equal(2, result.ScansFailed);
        Assert.Equal(0, result.ScansSynced);
        database.Verify(d => d.MarkScanSyncedAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SyncPendingScansAsync_NoPendingScans_NeverCallsTheApi()
    {
        // Arrange
        var apiClient = new Mock<IApexApiClient>();
        var database = new Mock<ICourierDatabase>();
        database.Setup(d => d.GetPendingScansAsync()).ReturnsAsync(Array.Empty<LocalScanDocument>());
        var sut = CreateSut(apiClient, database);

        // Act
        var result = await sut.SyncPendingScansAsync();

        // Assert
        Assert.True(result.IsSuccess);
        apiClient.Verify(a => a.UploadScansAsync(It.IsAny<string>(), It.IsAny<ScanUploadRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncPendingScansAsync_CallerCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var pending = new[] { NewScan("scan::001") };
        var apiClient = new Mock<IApexApiClient>();
        var database = new Mock<ICourierDatabase>();
        database.Setup(d => d.GetPendingScansAsync()).ReturnsAsync(pending);

        var sut = CreateSut(apiClient, database);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act + Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.SyncPendingScansAsync(cts.Token));
        apiClient.Verify(a => a.UploadScansAsync(It.IsAny<string>(), It.IsAny<ScanUploadRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        database.Verify(d => d.MarkScanSyncedAsync(It.IsAny<string>()), Times.Never);
        database.Verify(d => d.MarkScanFailedAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
