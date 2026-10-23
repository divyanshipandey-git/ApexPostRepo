
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class JsonInventoryRepository : IInventoryRepository
{
    private readonly JsonFileStore<InventoryStock> _store;

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
}
