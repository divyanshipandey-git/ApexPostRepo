using ApexPost.Api.DTOs;
using ApexPost.Api.Exceptions;
using ApexPost.Api.Services;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApexPost.Api.Tests;

public sealed class ScanUploadServiceTests
{
    private const string Tracking = "APX-20241107";
    private const string Courier = "COURIER-001";

    private static string NewScanId() => $"scan::{Guid.NewGuid()}";

    private static ScanDocumentRequest Scan(
        string? id = null,
        DTOs.ScanType type = DTOs.ScanType.Pickup,
        string courier = Courier,
        DateTimeOffset? at = null,
        string? notes = null,
        string? signature = null) => new()
    {
        ScanId = id ?? NewScanId(),
        ScanType = type,
        CourierId = courier,
        Location = "Warehouse A",
        ScannedAtUtc = at ?? DateTimeOffset.UtcNow,
        Notes = notes,
        RecipientSignature = signature
    };

    private static (ScanUploadService Service, FakeShipmentRepository Repository, FakeUnitOfWork Uow) Create(bool shipmentExists = true)
    {
        var repository = new FakeShipmentRepository(shipmentExists ? NewShipment() : null);
        var uow = new FakeUnitOfWork();
        return (new ScanUploadService(repository, uow, NullLogger<ScanUploadService>.Instance), repository, uow);
    }

    private static TrackedShipment NewShipment() => TrackedShipment.Create(
        trackingNumber: Tracking, weightKg: 1m, recipientName: "A", recipientPhone: "+15551234567",
        recipientAddress: "1 Main St", recipientCity: "Springfield", recipientPostalCode: null,
        recipientCountry: "US", specialInstructions: null, declaredValue: null,
        createdAtUtc: DateTimeOffset.UtcNow.AddMinutes(-30));

    [Fact]
    public async Task Upload_StoresNewScans()
    {
        var (service, _, uow) = Create();

        var response = await service.UploadScansAsync(Tracking, Courier, new ScanUploadRequest { Scans = [Scan()] });

        Assert.Equal(1, response.Accepted);
        Assert.Equal(0, response.DuplicatesSkipped);
        Assert.Equal(0, response.Rejected);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task Upload_SameScanIdTwice_SecondTimeIsDuplicate_NotRejection()
    {
        var (service, repository, _) = Create();
        var scan = Scan();

        await service.UploadScansAsync(Tracking, Courier, new ScanUploadRequest { Scans = [scan] });
        repository.CommitPendingScans();

        var second = await service.UploadScansAsync(Tracking, Courier, new ScanUploadRequest { Scans = [scan] });

        Assert.Equal(0, second.Accepted);
        Assert.Equal(1, second.DuplicatesSkipped);
        Assert.Equal(0, second.Rejected);
        Assert.Empty(second.RejectionDetails);   // duplicates are never mixed in with real rejections
        Assert.Equal(scan.ScanId, Assert.Single(second.DuplicateScanIds));
    }

    [Fact]
    public async Task Upload_SameScanIdTwiceInOneBatch_IsStoredOnce()
    {
        var (service, _, _) = Create();
        var id = NewScanId();

        var response = await service.UploadScansAsync(Tracking, Courier,
            new ScanUploadRequest { Scans = [Scan(id), Scan(id)] });

        Assert.Equal(1, response.Accepted);
        Assert.Equal(1, response.DuplicatesSkipped);
        Assert.Empty(response.RejectionDetails);
    }

    [Fact]
    public async Task Upload_ScanOfAnotherCourier_IsForbidden()
    {
        var (service, _, uow) = Create();

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UploadScansAsync(
            Tracking, Courier, new ScanUploadRequest { Scans = [Scan(courier: "COURIER-999")] }));

        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task Upload_WithoutCourierClaim_IsForbidden()
    {
        var (service, _, _) = Create();

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UploadScansAsync(
            Tracking, null, new ScanUploadRequest { Scans = [Scan()] }));
    }

    [Fact]
    public async Task Upload_UnknownShipment_IsNotFound()
    {
        var (service, _, _) = Create(shipmentExists: false);

        await Assert.ThrowsAsync<NotFoundException>(() => service.UploadScansAsync(
            Tracking, Courier, new ScanUploadRequest { Scans = [Scan()] }));
    }

    [Fact]
    public async Task Upload_FailedScanWithoutReason_IsRejectedWithReason_AndOthersAreStillAccepted()
    {
        var (service, _, _) = Create();

        var response = await service.UploadScansAsync(Tracking, Courier, new ScanUploadRequest
        {
            Scans = [Scan(), Scan(type: DTOs.ScanType.Failed, notes: null)]
        });

        Assert.Equal(1, response.Accepted);
        Assert.Equal(1, response.Rejected);
        Assert.Equal("Reason is required for Failed scans", Assert.Single(response.RejectionDetails).Reason);
    }

    [Fact]
    public async Task Upload_ScanInTheFuture_IsRejected()
    {
        var (service, _, _) = Create();

        var response = await service.UploadScansAsync(Tracking, Courier, new ScanUploadRequest
        {
            Scans = [Scan(at: DateTimeOffset.UtcNow.AddHours(2))]
        });

        Assert.Equal(0, response.Accepted);
        Assert.Equal("Scan time cannot be in the future", Assert.Single(response.RejectionDetails).Reason);
    }

    [Fact]
    public async Task Upload_WhenStoredConcurrentlyByAnotherRequest_ReturnsConflict()
    {
        var (service, _, uow) = Create();
        uow.ThrowUniqueKeyConflict = true;

        await Assert.ThrowsAsync<ConflictException>(() => service.UploadScansAsync(
            Tracking, Courier, new ScanUploadRequest { Scans = [Scan()] }));
    }

    // ---------------------------------------------------------------- fakes

    private sealed class FakeShipmentRepository(TrackedShipment? shipment) : ITrackedShipmentRepository
    {
        public Task<TrackedShipment?> GetByTrackingNumberWithTrackingAsync(string trackingNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(shipment);

        public Task<IReadOnlyList<TrackedShipmentScan>> GetScansByIdsAsync(IEnumerable<string> scanIds, CancellationToken cancellationToken = default)
        {
            var ids = scanIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<TrackedShipmentScan> found = shipment is null
                ? []
                : shipment.Scans.Where(s => ids.Contains(s.Id) && _committed.Contains(s.Id)).ToList();
            return Task.FromResult(found);
        }

        // The service adds scans to the tracked entity; "committed" mimics what the database really holds.
        private readonly HashSet<string> _committed = new(StringComparer.OrdinalIgnoreCase);
        public void CommitPendingScans()
        {
            foreach (var scan in shipment!.Scans) _committed.Add(scan.Id);
        }

        public Task UpdateAsync(TrackedShipment item, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TrackedShipment?> GetByIdAsync(string shipmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrackedShipment?> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TrackedShipment>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(TrackedShipment item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(TrackedShipment item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IQueryable<TrackedShipment> Query() => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public bool ThrowUniqueKeyConflict { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowUniqueKeyConflict)
            {
                throw new UniqueKeyConflictException("duplicate", new Exception("PK_TrackedShipmentScans"));
            }

            SaveCount++;
            return Task.FromResult(1);
        }

        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }
}
