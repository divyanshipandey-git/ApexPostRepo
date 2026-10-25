using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services.Staff;

public sealed record AuthenticatedStaff(
    string Id,
    StaffRole Role,
    string BranchCode);
