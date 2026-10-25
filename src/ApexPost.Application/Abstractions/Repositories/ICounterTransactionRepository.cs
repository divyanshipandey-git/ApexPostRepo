
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions.Repositories;

public interface ICounterTransactionRepository
{
    Task<CounterTransaction?> GetByIdAsync(
        string transactionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CounterTransaction>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        CounterTransaction transaction,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        CounterTransaction transaction,
        CancellationToken cancellationToken = default);
}
