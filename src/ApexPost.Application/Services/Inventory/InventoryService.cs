using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        cancellationToken.ThrowIfCancellationRequested();

        return await _inventoryRepository.GetByBranchAndSkuAsync(
            branchCode.Trim().ToUpperInvariant(),
            sku.Trim().ToUpperInvariant(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryStock>> GetBranchStockAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);

        cancellationToken.ThrowIfCancellationRequested();

        return await _inventoryRepository.GetByBranchAsync(
            branchCode.Trim().ToUpperInvariant(),
            cancellationToken);
    }
}