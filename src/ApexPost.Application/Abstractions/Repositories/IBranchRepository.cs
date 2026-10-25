
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Abstractions.Repositories;

public interface IBranchRepository
{
    Task<Branch?> GetByCodeAsync(
        BranchCode code,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Branch>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Branch branch,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Branch branch,
        CancellationToken cancellationToken = default);
}
