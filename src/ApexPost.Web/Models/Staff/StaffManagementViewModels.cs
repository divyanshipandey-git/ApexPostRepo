using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Entities;

namespace ApexPost.Web.Models.Staff;

public sealed class StaffManagementViewModel
{
    public string? StaffId { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public StaffRole Role { get; set; }

    [Required, StringLength(10)]
    public string BranchCode { get; set; } = string.Empty;

    [StringLength(256, MinimumLength = 12)]
    public string? InitialPassword { get; set; }

    public IReadOnlyList<string> BranchCodes { get; set; } = [];
    public IReadOnlyList<StaffRowViewModel> Staff { get; set; } = [];
}

public sealed record StaffRowViewModel(
    string Id,
    string Name,
    string Email,
    StaffRole Role,
    string BranchCode,
    bool IsActive);
