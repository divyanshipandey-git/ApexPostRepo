using ApexPost.Application.Abstractions.Repositories;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class JsonStaffCredentialRepository : IStaffCredentialRepository
{
    private readonly JsonFileStore<StaffCredential> _store;

    public JsonStaffCredentialRepository(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        _store = new JsonFileStore<StaffCredential>(
            Path.Combine(dataDirectory, "staff-credentials.json"),
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

    public Task AddAsync(
        StaffCredential credential,
        CancellationToken cancellationToken = default) =>
        _store.AddAsync(credential, cancellationToken);
}
