
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions.Repositories;

public interface IInventoryRepository
{
    Task<InventoryReservationResult> TryReserveAsync(
        string branchCode,
        IReadOnlyDictionary<string, int> skuQuantities,
        CancellationToken cancellationToken = default);

    Task RestoreReservationAsync(
        InventoryReservation reservation,
        CancellationToken cancellationToken = default);

    Task CommitReservationAsync(
        InventoryReservation reservation,
        CancellationToken cancellationToken = default);

    Task<InventoryStock?> GetByBranchAndSkuAsync(
        string branchCode,
        string sku,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryStock>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        InventoryStock stock,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        InventoryStock stock,
        CancellationToken cancellationToken = default);
}

public enum InventoryReservationStatus
{
    Reserved,
    StockUnavailable
}

public sealed record InventoryReservationResult(
    InventoryReservationStatus Status,
    InventoryReservation? Reservation = null);

public sealed record InventoryReservation(
    Guid Id,
    string BranchCode,
    IReadOnlyDictionary<string, int> SkuQuantities);
