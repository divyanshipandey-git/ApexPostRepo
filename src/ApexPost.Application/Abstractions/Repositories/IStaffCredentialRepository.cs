namespace ApexPost.Application.Abstractions.Repositories;

public interface IStaffCredentialRepository
{
    Task<string?> GetPasswordHashAsync(
        string staffId,
        CancellationToken cancellationToken = default);
}
