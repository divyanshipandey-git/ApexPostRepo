
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

    Task RestoreReversedRetailStockAsync(
        string transactionId,
        string branchCode,
        IReadOnlyDictionary<string, int> skuQuantities,
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

    async Task<InventoryStock?> AdjustQuantityAsync(
        string branchCode,
        string sku,
        int adjustment,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        if (adjustment == 0 || adjustment == int.MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(adjustment));
        }

        var stock = await GetByBranchAndSkuAsync(branchCode, sku, cancellationToken);
        if (stock is null)
        {
            if (adjustment < 0)
            {
                return null;
            }

            stock = new InventoryStock(new ApexPost.Domain.ValueObjects.BranchCode(branchCode),
                new ApexPost.Domain.ValueObjects.Sku(sku), adjustment);
            await AddAsync(stock, cancellationToken);
            return stock;
        }

        if (adjustment > 0)
        {
            stock.AddStock(adjustment);
        }
        else
        {
            stock.RemoveStock(-adjustment);
        }

        await UpdateAsync(stock, cancellationToken);
        return stock;
    }
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
