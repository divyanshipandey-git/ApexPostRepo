
using ApexPost.Domain.ValueObjects;
using System.Text.Json.Serialization;

namespace ApexPost.Domain.Entities;

public sealed class Staff
{
    public string Id { get; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public StaffRole Role { get; private set; }
    public BranchCode HomeBranchCode { get; private set; }
    [JsonInclude]
    public bool IsActive { get; private set; }

    public Staff(
        string id,
        string name,
        string email,
        StaffRole role,
        BranchCode homeBranchCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentNullException.ThrowIfNull(homeBranchCode);

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        Id = id.Trim();
        Name = name.Trim();
        Email = email.Trim();
        Role = role;
        HomeBranchCode = homeBranchCode;
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void UpdateProfile(string name, string email, StaffRole role, BranchCode homeBranchCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentNullException.ThrowIfNull(homeBranchCode);
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        Name = name.Trim();
        Email = email.Trim();
        Role = role;
        HomeBranchCode = homeBranchCode;
    }
}
