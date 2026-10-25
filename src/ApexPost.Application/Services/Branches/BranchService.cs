
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services.Branches;

public sealed class BranchService
{
    private readonly IBranchRepository _branchRepository;

    public BranchService(IBranchRepository branchRepository)
    {
        ArgumentNullException.ThrowIfNull(branchRepository);
        _branchRepository = branchRepository;
    }

    public async Task<Branch?> GetByCodeAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);

        var code = new BranchCode(branchCode.Trim().ToUpperInvariant());

        return await _branchRepository.GetByCodeAsync(
            code,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Branch>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _branchRepository.GetAllAsync(cancellationToken);
    }

    public async Task<bool> IsBranchOpenAsync(
        string branchCode,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);

        var branch = await GetByCodeAsync(branchCode, cancellationToken);

        return branch is { IsActive: true } &&
               branch.IsOpenAt(TimeOnly.FromDateTime(at.DateTime));
    }
}
