
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions.Repositories;

public interface IInventoryRepository
{
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
