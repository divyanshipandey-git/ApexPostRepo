using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions.Repositories;

public interface ITrackedShipmentRepository
{
    Task<TrackedShipment?> GetByIdAsync(
        string shipmentId,
        CancellationToken cancellationToken = default);

    Task<TrackedShipment?> GetByTrackingNumberAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackedShipment>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        TrackedShipment shipment,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        TrackedShipment shipment,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        TrackedShipment shipment,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackedShipmentScan>> GetScansByIdsAsync(
        IEnumerable<string> scanIds,
        CancellationToken cancellationToken = default);

    IQueryable<TrackedShipment> Query();
}