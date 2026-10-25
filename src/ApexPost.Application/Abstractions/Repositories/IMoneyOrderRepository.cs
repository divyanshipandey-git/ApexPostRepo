
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions.Repositories;

public interface IMoneyOrderRepository
{
    Task<MoneyOrder?> GetByIdAsync(
        string moneyOrderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MoneyOrder>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        MoneyOrder moneyOrder,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        MoneyOrder moneyOrder,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string moneyOrderId,
        CancellationToken cancellationToken = default);
}
