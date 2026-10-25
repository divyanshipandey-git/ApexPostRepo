
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions.Repositories;

public interface IStaffRepository
{
    Task<Staff?> GetByIdAsync(
        string staffId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Staff>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Staff>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Staff staff,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Staff staff,
        CancellationToken cancellationToken = default);
}
