
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class JsonShipmentRepository : IShipmentRepository
{
    private readonly JsonFileStore<Shipment> _store;

    public JsonShipmentRepository(JsonPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _store = new JsonFileStore<Shipment>(
            options.GetFilePath("shipments.json"),
            shipment => shipment.Id);
    }

    public Task<Shipment?> GetByIdAsync(
        string shipmentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shipmentId);

        return _store.GetByKeyAsync(
            shipmentId.Trim(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<Shipment>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);

        var shipments = await _store.GetAllAsync(cancellationToken);

        return shipments
            .Where(shipment => string.Equals(
                JsonEntityPropertyReader.GetBranchCode(shipment),
                branchCode.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public Task AddAsync(
        Shipment shipment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        return _store.AddAsync(shipment, cancellationToken);
    }

    public Task UpdateAsync(
        Shipment shipment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        return _store.UpdateAsync(shipment, cancellationToken);
    }
}
