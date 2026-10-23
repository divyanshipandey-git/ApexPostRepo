
using ApexPost.Application.Abstractions.Repositories;
using StaffEntity = ApexPost.Domain.Entities.Staff;

namespace ApexPost.Application.Services.Staff;

public sealed class StaffService
{
    private readonly IStaffRepository _staffRepository;

    public StaffService(IStaffRepository staffRepository)
    {
        ArgumentNullException.ThrowIfNull(staffRepository);
        _staffRepository = staffRepository;
    }

    public async Task<StaffEntity?> GetByIdAsync(
        string staffId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(staffId);

        return await _staffRepository.GetByIdAsync(
            staffId.Trim(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<StaffEntity>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _staffRepository.GetAllAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StaffEntity>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);

        return await _staffRepository.GetByBranchAsync(
            branchCode.Trim().ToUpperInvariant(),
            cancellationToken);
    }

    public async Task<bool> SetActiveStatusAsync(
        string staffId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(staffId);

        var staff = await _staffRepository.GetByIdAsync(
            staffId.Trim(),
            cancellationToken);

        if (staff is null)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (isActive)
        {
            staff.Activate();
        }
        else
        {
            staff.Deactivate();
        }

        await _staffRepository.UpdateAsync(staff, cancellationToken);
        return true;
    }
}
