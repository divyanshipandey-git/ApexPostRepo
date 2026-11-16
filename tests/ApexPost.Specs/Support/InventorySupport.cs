using ApexPost.Application.Abstractions.Notifications;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Inventory;
using ApexPost.Domain.Entities;

namespace ApexPost.Specs.Support;

/// <summary>In-memory stock store: one dictionary per scenario, so nothing leaks between scenarios.</summary>
public sealed class InMemoryInventoryRepository : IInventoryRepository
{
    private readonly Dictionary<(string Branch, string Sku), InventoryStock> _stock = [];

    public Task<InventoryStock?> GetByBranchAndSkuAsync(string branchCode, string sku, CancellationToken cancellationToken = default) =>
        Task.FromResult(_stock.GetValueOrDefault((branchCode, sku)));

    public Task<IReadOnlyList<InventoryStock>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<InventoryStock>>(
            _stock.Where(entry => entry.Key.Branch == branchCode).Select(entry => entry.Value).ToList());

    public Task AddAsync(InventoryStock stock, CancellationToken cancellationToken = default)
    {
        _stock[(stock.BranchCode.Value, stock.Sku.Value)] = stock;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(InventoryStock stock, CancellationToken cancellationToken = default)
    {
        _stock[(stock.BranchCode.Value, stock.Sku.Value)] = stock;
        return Task.CompletedTask;
    }

    // The reservation workflow belongs to the checkout flow and is not used by these scenarios.
    public Task<InventoryReservationResult> TryReserveAsync(string branchCode, IReadOnlyDictionary<string, int> skuQuantities, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task RestoreReservationAsync(InventoryReservation reservation, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task RestoreReversedRetailStockAsync(string transactionId, string branchCode, IReadOnlyDictionary<string, int> skuQuantities, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task CommitReservationAsync(InventoryReservation reservation, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>Remembers the low-stock alerts the service raised, so a scenario can check them.</summary>
public sealed class RecordingNotifications : INotificationService
{
    public List<(string Branch, string Sku, int Quantity)> LowStockAlerts { get; } = [];

    public Task<bool> NotifyLowStockAsync(string branchCode, string sku, int currentQuantity, int reorderLevel, CancellationToken cancellationToken = default)
    {
        LowStockAlerts.Add((branchCode, sku, currentQuantity));
        return Task.FromResult(true);
    }

    public Task<bool> NotifyHighValueMoneyOrderAsync(string branchCode, string moneyOrderId, decimal amount, decimal verificationThreshold, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task NotifyTransactionVoidAsync(string branchCode, string transactionId, string reason, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyTransactionRefundAsync(string branchCode, string transactionId, string reason, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

/// <summary>State of ONE inventory scenario (created per scenario by Reqnroll context injection).</summary>
public sealed class InventoryScenarioContext
{
    public InMemoryInventoryRepository Stock { get; } = new();

    public RecordingNotifications Notifications { get; } = new();

    public InventoryStock? LastStock { get; set; }

    public Exception? LastError { get; set; }

    public InventoryService CreateService() => new(Stock, Notifications);
}
