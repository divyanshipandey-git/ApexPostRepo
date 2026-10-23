
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions.Repositories;

public interface IShipmentRepository
{
    Task<Shipment?> GetByIdAsync(
        string shipmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Shipment>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Shipment shipment,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Shipment shipment,
        CancellationToken cancellationToken = default);
}
