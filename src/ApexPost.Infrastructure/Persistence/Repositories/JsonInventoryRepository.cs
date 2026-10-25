
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class JsonInventoryRepository : IInventoryRepository
{
    private readonly JsonFileStore<InventoryStock> _store;
    private readonly ConcurrentDictionary<Guid, InventoryReservation> _pendingReservations = new();
    private readonly SemaphoreSlim _reservationGate = new(1, 1);

    public JsonInventoryRepository(JsonPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _store = new JsonFileStore<InventoryStock>(
            options.GetFilePath("inventory.json"),
            stock => $"{stock.BranchCode.Value}|{stock.Sku.Value}");
    }

    public async Task<InventoryStock?> GetByBranchAndSkuAsync(
        string branchCode,
        string sku,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        return await _store.GetByKeyAsync(
            $"{branchCode.Trim()}|{sku.Trim()}",
            cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryStock>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);

        var inventory = await _store.GetAllAsync(cancellationToken);

        return inventory
            .Where(stock => string.Equals(
                stock.BranchCode.Value,
                branchCode.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public async Task<InventoryReservationResult> TryReserveAsync(
        string branchCode,
        IReadOnlyDictionary<string, int> skuQuantities,
        CancellationToken cancellationToken = default)
    {
        var (normalizedBranch, normalizedQuantities) = ValidateRequest(branchCode, skuQuantities);
        var reserved = await _store.MutateAllAsync(stocks =>
        {
            var selected = new Dictionary<string, InventoryStock>(StringComparer.OrdinalIgnoreCase);
            foreach (var (sku, quantity) in normalizedQuantities)
            {
                var stock = stocks.FirstOrDefault(item =>
                    string.Equals(item.BranchCode.Value, normalizedBranch, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.Sku.Value, sku, StringComparison.OrdinalIgnoreCase));
                if (stock is null || stock.Quantity < quantity)
                {
                    return (false, false);
                }

                selected.Add(sku, stock);
            }

            foreach (var (sku, quantity) in normalizedQuantities)
            {
                selected[sku].RemoveStock(quantity);
            }

            return (true, true);
        }, cancellationToken);

        if (!reserved)
        {
            return new InventoryReservationResult(InventoryReservationStatus.StockUnavailable);
        }

        var reservation = new InventoryReservation(
            Guid.NewGuid(),
            normalizedBranch,
            new ReadOnlyDictionary<string, int>(normalizedQuantities));
        if (!_pendingReservations.TryAdd(reservation.Id, reservation))
        {
            throw new InvalidOperationException("A stock reservation could not be tracked.");
        }

        return new InventoryReservationResult(InventoryReservationStatus.Reserved, reservation);
    }

    public async Task RestoreReservationAsync(
        InventoryReservation reservation,
        CancellationToken cancellationToken = default)
    {
        var validated = ValidateReservation(reservation);
        await _reservationGate.WaitAsync(cancellationToken);
        try
        {
            EnsurePendingReservation(validated);
            await _store.MutateAllAsync(stocks =>
            {
                var selected = new Dictionary<string, InventoryStock>(StringComparer.OrdinalIgnoreCase);
                foreach (var (sku, quantity) in validated.SkuQuantities)
                {
                    var stock = stocks.FirstOrDefault(item =>
                        string.Equals(item.BranchCode.Value, validated.BranchCode, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(item.Sku.Value, sku, StringComparison.OrdinalIgnoreCase));
                    if (stock is null || stock.Quantity > int.MaxValue - quantity)
                    {
                        throw new InvalidOperationException("Reserved stock could not be restored safely.");
                    }

                    selected.Add(sku, stock);
                }

                foreach (var (sku, quantity) in validated.SkuQuantities)
                {
                    selected[sku].AddStock(quantity);
                }

                return (true, true);
            }, cancellationToken);
            _pendingReservations.TryRemove(validated.Id, out _);
        }
        finally
        {
            _reservationGate.Release();
        }
    }

    public async Task CommitReservationAsync(
        InventoryReservation reservation,
        CancellationToken cancellationToken = default)
    {
        var validated = ValidateReservation(reservation);
        await _reservationGate.WaitAsync(cancellationToken);
        try
        {
            EnsurePendingReservation(validated);
            _pendingReservations.TryRemove(validated.Id, out _);
        }
        finally
        {
            _reservationGate.Release();
        }
    }

    public Task AddAsync(
        InventoryStock stock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stock);
        return _store.AddAsync(stock, cancellationToken);
    }

    public Task UpdateAsync(
        InventoryStock stock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stock);
        return _store.UpdateAsync(stock, cancellationToken);
    }

    public Task<InventoryStock?> AdjustQuantityAsync(
        string branchCode,
        string sku,
        int adjustment,
        CancellationToken cancellationToken = default)
    {
        if (adjustment == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(adjustment), "Stock adjustment cannot be zero.");
        }

        var normalizedBranch = new BranchCode(branchCode).Value;
        var normalizedSku = new Sku(sku).Value;
        return _store.MutateAllAsync(stocks =>
        {
            var stock = stocks.FirstOrDefault(item =>
                string.Equals(item.BranchCode.Value, normalizedBranch, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Sku.Value, normalizedSku, StringComparison.OrdinalIgnoreCase));
            if (stock is null)
            {
                if (adjustment < 0)
                {
                    return (false, (InventoryStock?)null);
                }

                stock = new InventoryStock(new BranchCode(normalizedBranch), new Sku(normalizedSku), adjustment);
                stocks.Add(stock);
                return (true, (InventoryStock?)stock);
            }

            if (adjustment > 0)
            {
                stock.AddStock(adjustment);
            }
            else
            {
                stock.RemoveStock(checked(-adjustment));
            }

            return (true, (InventoryStock?)stock);
        }, cancellationToken);
    }

    private static (string BranchCode, Dictionary<string, int> Quantities) ValidateRequest(
        string branchCode,
        IReadOnlyDictionary<string, int> skuQuantities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        ArgumentNullException.ThrowIfNull(skuQuantities);
        if (skuQuantities.Count == 0)
        {
            throw new ArgumentException("At least one stock quantity is required.", nameof(skuQuantities));
        }

        var normalizedBranch = new BranchCode(branchCode).Value;
        var normalizedQuantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, quantity) in skuQuantities)
        {
            if (string.IsNullOrWhiteSpace(key) || quantity <= 0)
            {
                throw new ArgumentException("Each SKU and quantity must be valid.", nameof(skuQuantities));
            }

            var sku = new Sku(key).Value;
            try
            {
                normalizedQuantities[sku] = checked(normalizedQuantities.GetValueOrDefault(sku) + quantity);
            }
            catch (OverflowException exception)
            {
                throw new ArgumentException("The requested quantity is too large.", nameof(skuQuantities), exception);
            }
        }

        return (normalizedBranch, normalizedQuantities);
    }

    private static InventoryReservation ValidateReservation(InventoryReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (reservation.Id == Guid.Empty)
        {
            throw new ArgumentException("A valid reservation identifier is required.", nameof(reservation));
        }

        var (branchCode, quantities) = ValidateRequest(reservation.BranchCode, reservation.SkuQuantities);
        return new InventoryReservation(
            reservation.Id,
            branchCode,
            new ReadOnlyDictionary<string, int>(quantities));
    }

    private void EnsurePendingReservation(InventoryReservation reservation)
    {
        if (!_pendingReservations.TryGetValue(reservation.Id, out var pending) ||
            !string.Equals(pending.BranchCode, reservation.BranchCode, StringComparison.OrdinalIgnoreCase) ||
            pending.SkuQuantities.Count != reservation.SkuQuantities.Count ||
            pending.SkuQuantities.Any(pair =>
                !reservation.SkuQuantities.TryGetValue(pair.Key, out var quantity) || quantity != pair.Value))
        {
            throw new InvalidOperationException("The stock reservation is unknown or has already been completed.");
        }
    }
}
