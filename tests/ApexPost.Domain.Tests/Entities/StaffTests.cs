
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public class StaffTests
{
    [Fact]
    public void Constructor_ShouldCreateActiveStaffMember()
    {
        var staff = CreateStaff();

        Assert.Equal("S001", staff.Id);
        Assert.Equal("Aman Sharma", staff.Name);
        Assert.Equal("aman@example.com", staff.Email);
        Assert.Equal(StaffRole.CounterClerk, staff.Role);
        Assert.Equal("IND01", staff.HomeBranchCode.Value);
        Assert.True(staff.IsActive);
    }

    [Fact]
    public void Constructor_ShouldTrimStringValues()
    {
        var staff = new Staff(
            " S001 ",
            " Aman Sharma ",
            " aman@example.com ",
            StaffRole.CounterClerk,
            new BranchCode("IND01"));

        Assert.Equal("S001", staff.Id);
        Assert.Equal("Aman Sharma", staff.Name);
        Assert.Equal("aman@example.com", staff.Email);
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyName()
    {
        Assert.Throws<ArgumentException>(() =>
            new Staff(
                "S001",
                " ",
                "aman@example.com",
                StaffRole.CounterClerk,
                new BranchCode("IND01")));
    }

    [Fact]
    public void Constructor_ShouldRejectUndefinedRole()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Staff(
                "S001",
                "Aman Sharma",
                "aman@example.com",
                (StaffRole)999,
                new BranchCode("IND01")));
    }

    [Fact]
    public void Deactivate_ShouldMakeStaffInactive()
    {
        var staff = CreateStaff();

        staff.Deactivate();

        Assert.False(staff.IsActive);
    }

    [Fact]
    public void Activate_ShouldReactivateStaff()
    {
        var staff = CreateStaff();
        staff.Deactivate();

        staff.Activate();

        Assert.True(staff.IsActive);
    }

    private static Staff CreateStaff() =>
        new(
            "S001",
            "Aman Sharma",
            "aman@example.com",
            StaffRole.CounterClerk,
            new BranchCode("IND01"));
}
