
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class JsonBranchRepository : IBranchRepository
{
    private readonly JsonFileStore<Branch> _store;

    public JsonBranchRepository(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        _store = new JsonFileStore<Branch>(
            Path.Combine(JsonPersistenceOptions.ResolveDataDirectory(dataDirectory), "branches.json"),
            branch => branch.Code.Value);
    }

    public Task<Branch?> GetByCodeAsync(
        BranchCode code,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _store.GetByKeyAsync(code.Value, cancellationToken);
    }

    public Task<IReadOnlyList<Branch>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        _store.GetAllAsync(cancellationToken);

    public Task AddAsync(
        Branch branch,
        CancellationToken cancellationToken = default) =>
        _store.AddAsync(branch, cancellationToken);

    public Task UpdateAsync(
        Branch branch,
        CancellationToken cancellationToken = default) =>
        _store.UpdateAsync(branch, cancellationToken);
}
