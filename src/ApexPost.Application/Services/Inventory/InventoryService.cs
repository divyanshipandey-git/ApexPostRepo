using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Application.Abstractions.Notifications;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services.Inventory;

public sealed class InventoryService
{
    private const int LowStockReorderLevel = 5;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly INotificationService? _notifications;
    private readonly ApexPost.Application.Abstractions.Persistence.IUnitOfWork? _unitOfWork;

    public InventoryService(IInventoryRepository inventoryRepository)
        : this(inventoryRepository, null)
    {
    }

    public InventoryService(IInventoryRepository inventoryRepository, INotificationService? notifications, ApexPost.Application.Abstractions.Persistence.IUnitOfWork? unitOfWork = null)
    {
        ArgumentNullException.ThrowIfNull(inventoryRepository);
        _inventoryRepository = inventoryRepository;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<InventoryStock?> GetStockAsync(
        string branchCode,
        string sku,
        CancellationToken cancellationToken = default)
    {
        var normalizedBranchCode = ValidateBranchCode(branchCode);
        var normalizedSku = ValidateSku(sku);

        cancellationToken.ThrowIfCancellationRequested();

        return await _inventoryRepository.GetByBranchAndSkuAsync(
            normalizedBranchCode,
            normalizedSku,
            cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryStock>> GetBranchStockAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedBranchCode = ValidateBranchCode(branchCode);

        cancellationToken.ThrowIfCancellationRequested();

        return await _inventoryRepository.GetByBranchAsync(
            normalizedBranchCode,
            cancellationToken);
    }

    public Task<InventoryStock?> AdjustQuantityAsync(
        string branchCode,
        string sku,
        int adjustment,
        CancellationToken cancellationToken = default)
        => AdjustQuantityAsync(branchCode, sku, adjustment, "Stock adjustment from Stock Management", "Unknown", cancellationToken);

    public Task<InventoryStock?> AdjustQuantityAsync(
        string branchCode,
        string sku,
        int adjustment,
        string reason,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var normalizedBranchCode = ValidateBranchCode(branchCode);
        var normalizedSku = ValidateSku(sku);
        if (adjustment == 0 || adjustment == int.MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(adjustment), "Use a non-zero quantity within the supported range.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(changedBy);
        return AdjustQuantityCoreAsync(normalizedBranchCode, normalizedSku, adjustment, reason.Trim(), changedBy.Trim(), cancellationToken);
    }

    private async Task<InventoryStock?> AdjustQuantityCoreAsync(
        string branchCode,
        string sku,
        int adjustment,
        string reason,
        string changedBy,
        CancellationToken cancellationToken)
    {
        var stock = await _inventoryRepository.AdjustQuantityWithAuditAsync(branchCode, sku, adjustment, reason, changedBy, cancellationToken);
        if (_unitOfWork is not null && stock is not null) await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (stock is not null && stock.Quantity <= LowStockReorderLevel && _notifications is not null)
        {
            await _notifications.NotifyLowStockAsync(
                branchCode, sku, stock.Quantity, LowStockReorderLevel, CancellationToken.None);
        }

        return stock;
    }

    private static string ValidateBranchCode(string branchCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        return new BranchCode(branchCode).Value;
    }

    private static string ValidateSku(string sku)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        return new Sku(sku).Value;
    }
}
