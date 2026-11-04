using ApexPost.Application.Abstractions.Repositories;

namespace ApexPost.Infrastructure.Persistence.Repositories;

/// <summary>
/// JSON file-backed implementation of <see cref="IStaffCredentialRepository"/> for import/test scenarios only.
/// Does NOT provide SQL transaction guarantees, concurrency handling, or audit persistence.
/// Use <see cref="SqlStaffCredentialRepository"/> for all SQL-backed workflows.
/// </summary>
public sealed class JsonStaffCredentialRepository : IStaffCredentialRepository
{
    private readonly JsonFileStore<StaffCredential> _store;

    public JsonStaffCredentialRepository(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        _store = new JsonFileStore<StaffCredential>(
            Path.Combine(JsonPersistenceOptions.ResolveDataDirectory(dataDirectory), "staff-credentials.json"),
            credential => credential.StaffId);
    }

    public async Task<string?> GetPasswordHashAsync(
        string staffId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(staffId);

        var credential = await _store.GetByKeyAsync(staffId, cancellationToken);
        return credential?.PasswordHash;
    }

    public Task AddPasswordHashAsync(
        string staffId,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(staffId);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        return _store.AddAsync(new StaffCredential(staffId.Trim(), passwordHash), cancellationToken);
    }

    public Task AddAsync(
        StaffCredential credential,
        CancellationToken cancellationToken = default) =>
        _store.AddAsync(credential, cancellationToken);
}
