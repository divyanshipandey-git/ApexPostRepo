using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Security;

namespace ApexPost.Application.Services.Staff;

public sealed class StaffAuthenticationService
{
    private readonly IStaffRepository _staffRepository;
    private readonly IStaffCredentialRepository _credentialRepository;
    private readonly IStaffPasswordHasher _passwordHasher;

    public StaffAuthenticationService(
        IStaffRepository staffRepository,
        IStaffCredentialRepository credentialRepository,
        IStaffPasswordHasher passwordHasher)
    {
        ArgumentNullException.ThrowIfNull(staffRepository);
        ArgumentNullException.ThrowIfNull(credentialRepository);
        ArgumentNullException.ThrowIfNull(passwordHasher);

        _staffRepository = staffRepository;
        _credentialRepository = credentialRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthenticatedStaff?> AuthenticateAsync(
        string? email,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var staff = string.IsNullOrWhiteSpace(email)
            ? null
            : (await _staffRepository.GetAllAsync(cancellationToken))
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Email,
                    email.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        var hash = staff is null
            ? null
            : await _credentialRepository.GetPasswordHashAsync(staff.Id, cancellationToken);

        var passwordMatches = _passwordHasher.VerifyPassword(password ?? string.Empty, hash);

        if (staff is null || !staff.IsActive || !passwordMatches)
        {
            return null;
        }

        return new AuthenticatedStaff(
            staff.Id,
            staff.Role,
            staff.HomeBranchCode.Value);
    }
}
