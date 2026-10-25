namespace ApexPost.Application.Abstractions.Repositories;

public interface IStaffCredentialRepository
{
    Task AddPasswordHashAsync(
        string staffId,
        string passwordHash,
        CancellationToken cancellationToken = default);

    Task<string?> GetPasswordHashAsync(
        string staffId,
        CancellationToken cancellationToken = default);
}
