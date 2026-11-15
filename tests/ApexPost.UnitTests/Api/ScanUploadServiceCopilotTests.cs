// COPILOT-ASSISTED: drafted with GitHub Copilot (prompt 1 in COPILOT-LOG.md), then reviewed by the student.
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
using DtoScanType = ApexPost.Api.DTOs.ScanType;

namespace ApexPost.UnitTests.Api;

public sealed class ScanUploadServiceCopilotTests
{
    private const string Tracking = "APX-20250101";
    private const string Courier = "COURIER-001";
    private static readonly DateTimeOffset Now = new(2025, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITrackedShipmentRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ManualTimeProvider _timeProvider = new(Now);
    // Reviewed: Copilot created the shipment inline, so no test could look at the scans that were really stored. Kept in a field.
    private readonly TrackedShipment _shipment = CreateShipment();

    public ScanUploadServiceCopilotTests()
    {
        _repository.Setup(r => r.GetByTrackingNumberWithTrackingAsync(Tracking, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_shipment);
        _repository.Setup(r => r.GetScansByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private ScanUploadService CreateSut() =>
        new(_repository.Object, _unitOfWork.Object, NullLogger<ScanUploadService>.Instance, _timeProvider);

    private static TrackedShipment CreateShipment() =>
        TrackedShipment.Create(
            Tracking,
            2.5m,
            "Jane Example",
            "+15550100",
            "1 Example Street",
            "Springfield",
            createdAtUtc: Now.AddHours(-2));

    private static ScanDocumentRequest Scan(
        string? id = null,
        DtoScanType type = DtoScanType.Pickup,
        string courierId = Courier,
        DateTimeOffset? at = null,
        string? notes = null,
        string? signature = null) => new()
    {
        ScanId = id ?? $"scan::{Guid.NewGuid()}",
        ScanType = type,
        CourierId = courierId,
        Location = "Depot A",
        ScannedAtUtc = at ?? Now.AddMinutes(-10),
        Notes = notes,
        RecipientSignature = signature
    };

    private static ScanUploadRequest Batch(params ScanDocumentRequest[] scans) => new() { Scans = [.. scans] };

    [Fact]
    public async Task UploadScansAsync_NewScan_IsAcceptedAndSavedOnce()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.UploadScansAsync(Tracking, Courier, Batch(Scan()));

        // Assert
        Assert.Equal(1, result.Accepted);
        Assert.Equal(0, result.DuplicatesSkipped);
        Assert.Equal(0, result.Rejected);
        // Reviewed: Copilot used It.IsAny<TrackedShipment>() here; the exact shipment instance is what must be updated.
        _repository.Verify(r => r.UpdateAsync(_shipment, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadScansAsync_StoredScanIdIsSkippedAndNothingIsSaved()
    {
        // Arrange
        var scan = Scan(id: "scan::already-there");
        _repository.Setup(r => r.GetScansByIdsAsync(
                It.Is<IEnumerable<string>>(ids => ids.Contains("scan::already-there")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TrackedShipmentScan { Id = "scan::already-there" }]);

        // Act
        var result = await CreateSut().UploadScansAsync(Tracking, Courier, Batch(scan));

        // Assert
        Assert.Equal(0, result.Accepted);
        Assert.Equal(1, result.DuplicatesSkipped);
        Assert.Equal(0, result.Rejected);
        Assert.Equal(["scan::already-there"], result.DuplicateScanIds);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<TrackedShipment>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadScansAsync_SameScanIdTwiceInOneBatch_AcceptsOnceAndCountsOneDuplicate()
    {
        // Arrange
        var request = Batch(Scan(id: "scan::twice"), Scan(id: "scan::twice"));

        // Act
        var result = await CreateSut().UploadScansAsync(Tracking, Courier, request);

        // Assert
        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.DuplicatesSkipped);
        Assert.Equal(0, result.Rejected);
        // Reviewed: Copilot only checked the counts; a service could report "1 accepted" and still store two scans.
        Assert.Single(_shipment.Scans);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadScansAsync_MixedBatchOfNewDuplicateAndInvalidScans_ReportsEachOutcomeSeparately()
    {
        // Arrange
        _repository.Setup(r => r.GetScansByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TrackedShipmentScan { Id = "scan::dup" }]);
        var request = Batch(
            Scan(id: "scan::ok"),
            Scan(id: "scan::dup"),
            Scan(id: "scan::future", at: Now.AddHours(1)));

        // Act
        var result = await CreateSut().UploadScansAsync(Tracking, Courier, request);

        // Assert
        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.DuplicatesSkipped);
        Assert.Equal(1, result.Rejected);
        Assert.Equal("scan::future", result.RejectionDetails[0].ScanId);
        Assert.Equal("Scan time cannot be in the future", result.RejectionDetails[0].Reason);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<TrackedShipment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public async Task UploadScansAsync_ScanTimeInTheFutureAtBoundaryMinutes_AppliesFiveMinuteSkew(int minutesAhead, bool accepted)
    {
        // Arrange
        var request = Batch(Scan(at: Now.AddMinutes(minutesAhead)));

        // Act
        var result = await CreateSut().UploadScansAsync(Tracking, Courier, request);

        // Assert
        Assert.Equal(accepted ? 1 : 0, result.Accepted);
        Assert.Equal(accepted ? 0 : 1, result.Rejected);
        if (!accepted)
        {
            Assert.Equal("Scan time cannot be in the future", result.RejectionDetails[0].Reason);
        }
    }

    [Fact]
    public async Task UploadScansAsync_DeliveredScanWithoutSignature_IsRejected()
    {
        // Arrange
        var request = Batch(Scan(type: DtoScanType.Delivered, signature: null));

        // Act
        var result = await CreateSut().UploadScansAsync(Tracking, Courier, request);

        // Assert
        Assert.Equal(0, result.Accepted);
        Assert.Equal(1, result.Rejected);
        Assert.Equal("Recipient signature is required for Delivered scans", result.RejectionDetails[0].Reason);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadScansAsync_ScanOfAnotherCourier_ThrowsForbiddenException()
    {
        // Arrange
        var sut = CreateSut();
        var request = Batch(Scan(courierId: "COURIER-999"));

        // Act + Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => sut.UploadScansAsync(Tracking, Courier, request));
        _repository.Verify(r => r.GetByTrackingNumberWithTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // Added in review: Copilot did not cover a token without a courierId claim.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadScansAsync_MissingCourierClaim_ThrowsForbiddenException(string? claim)
    {
        // Arrange
        var sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => sut.UploadScansAsync(Tracking, claim, Batch(Scan())));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadScansAsync_UnknownShipment_ThrowsNotFoundException()
    {
        // Arrange
        _repository.Setup(r => r.GetByTrackingNumberWithTrackingAsync("APX-20250999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedShipment?)null);
        var sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sut.UploadScansAsync("APX-20250999", Courier, Batch(Scan())));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadScansAsync_UniqueKeyRaceOnSave_ThrowsConflictException()
    {
        // Arrange
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UniqueKeyConflictException("duplicate key", new Exception("inner")));

        // Act + Assert
        await Assert.ThrowsAsync<ConflictException>(() => CreateSut().UploadScansAsync(Tracking, Courier, Batch(Scan())));
    }
}
