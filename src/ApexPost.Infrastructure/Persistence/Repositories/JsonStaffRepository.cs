
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class JsonStaffRepository : IStaffRepository
{
    private readonly JsonFileStore<Staff> _store;

    public JsonStaffRepository(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        _store = new JsonFileStore<Staff>(
            Path.Combine(JsonPersistenceOptions.ResolveDataDirectory(dataDirectory), "staff.json"),
            staff => staff.Id);
    }

    public Task<Staff?> GetByIdAsync(
        string staffId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(staffId);
        return _store.GetByKeyAsync(staffId.Trim(), cancellationToken);
    }

    public Task<IReadOnlyList<Staff>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        _store.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<Staff>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);

        var staff = await _store.GetAllAsync(cancellationToken);

        return staff
            .Where(item => string.Equals(
                item.HomeBranchCode.Value,
                branchCode.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public Task AddAsync(
        Staff staff,
        CancellationToken cancellationToken = default) =>
        _store.AddAsync(staff, cancellationToken);

    public Task UpdateAsync(
        Staff staff,
        CancellationToken cancellationToken = default) =>
        _store.UpdateAsync(staff, cancellationToken);
}
