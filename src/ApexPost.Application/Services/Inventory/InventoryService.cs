using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services.Inventory;

public sealed class InventoryService
{
    private readonly IInventoryRepository _inventoryRepository;

    public InventoryService(IInventoryRepository inventoryRepository)
    {
        ArgumentNullException.ThrowIfNull(inventoryRepository);
        _inventoryRepository = inventoryRepository;
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