using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class JsonCounterRepository : ICounterRepository
{
    private readonly JsonFileStore<Counter> _store;

    public JsonCounterRepository(JsonPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _store = new JsonFileStore<Counter>(options.GetFilePath("counters.json"), counter => counter.Id);
    }

    public Task<Counter?> GetByIdAsync(string counterId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(counterId);
        return _store.GetByKeyAsync(counterId.Trim(), cancellationToken);
    }

    public async Task<IReadOnlyList<Counter>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        var counters = await _store.GetAllAsync(cancellationToken);
        return counters.Where(counter => string.Equals(
            counter.BranchCode.Value,
            branchCode.Trim(),
            StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public Task AddAsync(Counter counter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(counter);
        return _store.AddAsync(counter, cancellationToken);
    }

    public Task UpdateAsync(Counter counter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(counter);
        return _store.UpdateAsync(counter, cancellationToken);
    }
}
