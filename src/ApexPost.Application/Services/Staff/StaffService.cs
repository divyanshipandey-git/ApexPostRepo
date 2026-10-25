
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Security;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using StaffEntity = ApexPost.Domain.Entities.Staff;

namespace ApexPost.Application.Services.Staff;

public sealed class StaffService
{
    private readonly IStaffRepository _staffRepository;
    private readonly IBranchRepository? _branchRepository;
    private readonly IStaffCredentialRepository? _credentialRepository;
    private readonly IStaffPasswordHasher? _passwordHasher;

    public StaffService(IStaffRepository staffRepository)
    {
        ArgumentNullException.ThrowIfNull(staffRepository);
        _staffRepository = staffRepository;
    }

    public StaffService(
        IStaffRepository staffRepository,
        IBranchRepository branchRepository,
        IStaffCredentialRepository credentialRepository,
        IStaffPasswordHasher passwordHasher)
        : this(staffRepository)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public async Task<StaffEntity> CreateAsync(
        string id,
        string name,
        string email,
        StaffRole role,
        string branchCode,
        string initialPassword,
        CancellationToken cancellationToken = default)
    {
        if (_branchRepository is null || _credentialRepository is null || _passwordHasher is null)
        {
            throw new InvalidOperationException("Staff provisioning dependencies are not configured.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(initialPassword);
        if (initialPassword.Length < 12 || initialPassword.Length > 256)
        {
            throw new ArgumentException("Initial password must contain between 12 and 256 characters.", nameof(initialPassword));
        }

        var normalizedId = id.Trim();
        var normalizedEmail = email.Trim();
        var code = new BranchCode(branchCode);
        var existing = await _staffRepository.GetAllAsync(cancellationToken);
        if (existing.Any(staff => string.Equals(staff.Id, normalizedId, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(staff.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A staff member with that ID or email already exists.");
        }

        if (await _branchRepository.GetByCodeAsync(code, cancellationToken) is not { IsActive: true })
        {
            throw new InvalidOperationException("Select an active branch.");
        }

        var staff = new StaffEntity(normalizedId, name, normalizedEmail, role, code);
        var passwordHash = _passwordHasher.HashPassword(initialPassword);
        await _staffRepository.AddAsync(staff, cancellationToken);
        try
        {
            await _credentialRepository.AddPasswordHashAsync(staff.Id, passwordHash, cancellationToken);
        }
        catch
        {
            // Keep the staff record visible for administrative recovery if credential storage fails.
            throw;
        }

        return staff;
    }

    public async Task<bool> UpdateProfileAsync(
        string staffId,
        string name,
        string email,
        StaffRole role,
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        if (_branchRepository is null)
        {
            throw new InvalidOperationException("Staff provisioning dependencies are not configured.");
        }

        var staff = await _staffRepository.GetByIdAsync(staffId.Trim(), cancellationToken);
        if (staff is null)
        {
            return false;
        }

        var code = new BranchCode(branchCode);
        if (await _branchRepository.GetByCodeAsync(code, cancellationToken) is not { IsActive: true })
        {
            throw new InvalidOperationException("Select an active branch.");
        }

        var allStaff = await _staffRepository.GetAllAsync(cancellationToken);
        if (allStaff.Any(candidate => candidate.Id != staff.Id && string.Equals(
                candidate.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A staff member with that email already exists.");
        }

        staff.UpdateProfile(name, email, role, code);
        await _staffRepository.UpdateAsync(staff, cancellationToken);
        return true;
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
