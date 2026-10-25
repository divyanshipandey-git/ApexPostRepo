using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions.Repositories;

public interface ICounterRepository
{
    Task<Counter?> GetByIdAsync(
        string counterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Counter>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Counter counter,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Counter counter,
        CancellationToken cancellationToken = default);
}
