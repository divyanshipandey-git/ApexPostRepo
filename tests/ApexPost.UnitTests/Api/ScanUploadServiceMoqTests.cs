using ApexPost.Api.DTOs;
using ApexPost.Api.Exceptions;
using ApexPost.Api.Services;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using DomainScanType = ApexPost.Domain.Entities.ScanType;
using DtoScanType = ApexPost.Api.DTOs.ScanType;

namespace ApexPost.UnitTests.Api;

// AI-ASSISTED: drafted with an AI assistant (Claude Code), then reviewed and changed by a human; see COPILOT-LOG.md.
/// <summary>
/// ScanUploadService (idempotent upload) with ITrackedShipmentRepository and IUnitOfWork mocked.
/// The mock proves WHICH calls the service makes (save once / never). It does not prove that the real
/// primary key on TrackedShipmentScans rejects a duplicate id: ApexPost.IntegrationTests does that on a real database.
/// </summary>
public sealed class ScanUploadServiceMoqTests
{
    private const string Tracking = "APX-20250101";
    private const string Courier = "COURIER-001";
    private static readonly DateTimeOffset Now = new(2025, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITrackedShipmentRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ManualTimeProvider _clock = new(Now);
    private readonly TrackedShipment _shipment = TrackedShipment.Create(
        Tracking, 2.5m, "Jane Example", "+15550100", "1 Example Street", "Springfield",
        createdAtUtc: Now.AddHours(-2));

    public ScanUploadServiceMoqTests()
    {
        _repository.Setup(r => r.GetByTrackingNumberWithTrackingAsync(Tracking, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_shipment);
        _repository.Setup(r => r.GetScansByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private ScanUploadService CreateSut() =>
        new(_repository.Object, _unitOfWork.Object, NullLogger<ScanUploadService>.Instance, _clock);

    private static ScanDocumentRequest Scan(
        string? id = null,
        DtoScanType type = DtoScanType.Pickup,
        string courier = Courier,
        DateTimeOffset? at = null,
        string? notes = null,
        string? signature = null) => new()
    {
        ScanId = id ?? $"scan::{Guid.NewGuid()}",
        ScanType = type,
        CourierId = courier,
        Location = "Depot A",
        ScannedAtUtc = at ?? Now.AddMinutes(-10),
        Notes = notes,
        RecipientSignature = signature
    };

    private static ScanUploadRequest Batch(params ScanDocumentRequest[] scans) => new() { Scans = [.. scans] };

    [Fact]
    public async Task UploadScansAsync_NewScan_IsAcceptedAndSavedExactlyOnce()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var response = await sut.UploadScansAsync(Tracking, Courier, Batch(Scan()));

        // Assert
        Assert.Equal(1, response.Accepted);
        _repository.Verify(r => r.UpdateAsync(_shipment, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(_shipment.Scans);
    }

    [Fact]
    public async Task UploadScansAsync_ScanIdAlreadyStored_IsSkippedAndNothingIsSaved()
    {
        // Arrange
        var scan = Scan(id: "scan::already-there");
        _repository.Setup(r => r.GetScansByIdsAsync(
                It.Is<IEnumerable<string>>(ids => ids.Contains("scan::already-there")), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TrackedShipmentScan { Id = "scan::already-there" }]);

        // Act
        var response = await CreateSut().UploadScansAsync(Tracking, Courier, Batch(scan));

        // Assert
        Assert.Equal(0, response.Accepted);
        Assert.Equal(1, response.DuplicatesSkipped);
        Assert.Equal(0, response.Rejected);
        Assert.Equal(["scan::already-there"], response.DuplicateScanIds);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<TrackedShipment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadScansAsync_SameScanIdTwiceInOneBatch_AcceptsOnceAndCountsOneDuplicate()
    {
        // Arrange
        var first = Scan(id: "scan::twice");
        var second = Scan(id: "scan::twice");

        // Act
        var response = await CreateSut().UploadScansAsync(Tracking, Courier, Batch(first, second));

        // Assert
        Assert.Equal(1, response.Accepted);
        Assert.Equal(1, response.DuplicatesSkipped);
        Assert.Single(_shipment.Scans);
    }

    [Fact]
    public async Task UploadScansAsync_MixedBatch_ReportsAcceptedDuplicateAndRejectedSeparately()
    {
        // Arrange
        _repository.Setup(r => r.GetScansByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TrackedShipmentScan { Id = "scan::dup" }]);
        var batch = Batch(
            Scan(id: "scan::ok"),
            Scan(id: "scan::dup"),
            Scan(id: "scan::future", at: Now.AddHours(1)));

        // Act
        var response = await CreateSut().UploadScansAsync(Tracking, Courier, batch);

        // Assert
        Assert.Equal(1, response.Accepted);
        Assert.Equal(1, response.DuplicatesSkipped);
        Assert.Equal(1, response.Rejected);
        var rejection = Assert.Single(response.RejectionDetails);
        Assert.Equal("scan::future", rejection.ScanId);
        Assert.Equal("Scan time cannot be in the future", rejection.Reason);
    }

    [Theory]
    [InlineData(4, true)]   // inside the 5 minute clock-skew allowance
    [InlineData(5, true)]   // exactly on the boundary: allowed
    [InlineData(6, false)]  // one minute past it: rejected
    public async Task UploadScansAsync_ScanTimeAroundFutureBoundary_AppliesFiveMinuteSkew(int minutesAhead, bool accepted)
    {
        // Arrange
        var scan = Scan(at: Now.AddMinutes(minutesAhead));

        // Act
        var response = await CreateSut().UploadScansAsync(Tracking, Courier, Batch(scan));

        // Assert
        Assert.Equal(accepted ? 1 : 0, response.Accepted);
        Assert.Equal(accepted ? 0 : 1, response.Rejected);
    }

    [Fact]
    public async Task UploadScansAsync_DeliveredWithoutSignature_IsRejectedWithReason()
    {
        // Arrange
        var scan = Scan(type: DtoScanType.Delivered, signature: null);

        // Act
        var response = await CreateSut().UploadScansAsync(Tracking, Courier, Batch(scan));

        // Assert
        Assert.Equal("Recipient signature is required for Delivered scans", Assert.Single(response.RejectionDetails).Reason);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadScansAsync_ScanOfAnotherCourier_ThrowsForbiddenAndTouchesNothing()
    {
        // Arrange
        var scan = Scan(courier: "COURIER-999");

        // Act + Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().UploadScansAsync(Tracking, Courier, Batch(scan)));
        _repository.Verify(r => r.GetByTrackingNumberWithTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadScansAsync_MissingCourierClaim_ThrowsForbidden(string? claim)
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().UploadScansAsync(Tracking, claim, Batch(Scan())));
    }

    [Fact]
    public async Task UploadScansAsync_UnknownShipment_ThrowsNotFound()
    {
        // Arrange
        _repository.Setup(r => r.GetByTrackingNumberWithTrackingAsync("APX-20250999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedShipment?)null);

        // Act + Assert
        await Assert.ThrowsAsync<NotFoundException>(() => CreateSut().UploadScansAsync("APX-20250999", Courier, Batch(Scan())));
    }

    [Fact]
    public async Task UploadScansAsync_ConcurrentUploadWinsTheRace_ThrowsConflictSoClientRetries()
    {
        // Arrange: the duplicate check passed, but the database primary key rejects the insert.
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UniqueKeyConflictException("duplicate key", new Exception("inner")));

        // Act + Assert
        await Assert.ThrowsAsync<ConflictException>(() => CreateSut().UploadScansAsync(Tracking, Courier, Batch(Scan())));
    }

    [Fact]
    public async Task UploadScansAsync_ClockMoves_UsesTheInjectedClockForTheFutureCheck()
    {
        // Arrange: a scan 10 minutes ahead is rejected now, but valid after the clock advances 10 minutes.
        var scan = Scan(at: Now.AddMinutes(10));
        var sut = CreateSut();
        var before = await sut.UploadScansAsync(Tracking, Courier, Batch(scan));

        // Act
        _clock.Advance(TimeSpan.FromMinutes(10));
        var after = await sut.UploadScansAsync(Tracking, Courier, Batch(scan));

        // Assert
        Assert.Equal(1, before.Rejected);
        Assert.Equal(1, after.Accepted);
    }

    [Fact]
    public void Constructor_NullRepository_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ScanUploadService(null!, _unitOfWork.Object, NullLogger<ScanUploadService>.Instance));
    }

    [Fact]
    public async Task UploadScansAsync_ScanTypeIsMappedToTheDomainEnum()
    {
        // Arrange
        var scan = Scan(type: DtoScanType.InTransit);

        // Act
        await CreateSut().UploadScansAsync(Tracking, Courier, Batch(scan));

        // Assert
        Assert.Equal(DomainScanType.InTransit, Assert.Single(_shipment.Scans).ScanType);
    }
}
