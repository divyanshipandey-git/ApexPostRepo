
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class JsonCounterTransactionRepository
    : ICounterTransactionRepository
{
    private readonly JsonFileStore<CounterTransaction> _store;

    public JsonCounterTransactionRepository(JsonPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _store = new JsonFileStore<CounterTransaction>(
            options.GetFilePath("counter-transactions.json"),
            transaction => transaction.Id);
    }

    public Task<CounterTransaction?> GetByIdAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

        return _store.GetByKeyAsync(
            transactionId.Trim(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<CounterTransaction>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);

        var transactions = await _store.GetAllAsync(cancellationToken);

        return transactions
            .Where(transaction => string.Equals(
                JsonEntityPropertyReader.GetBranchCode(transaction),
                branchCode.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public Task AddAsync(
        CounterTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return _store.AddAsync(transaction, cancellationToken);
    }

    public Task UpdateAsync(
        CounterTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return _store.UpdateAsync(transaction, cancellationToken);
    }
}
