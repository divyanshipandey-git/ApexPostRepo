
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public class BranchTests
{
    [Fact]
    public void Constructor_ShouldCreateActiveBranch()
    {
        var branch = new Branch(
            new BranchCode("IND01"),
            "Indore Main",
            "Central",
            new TimeOnly(9, 0),
            new TimeOnly(18, 0));

        Assert.Equal("IND01", branch.Code.Value);
        Assert.Equal("Indore Main", branch.Name);
        Assert.True(branch.IsActive);
    }

    [Fact]
    public void Constructor_ShouldRejectClosingTimeBeforeOpeningTime()
    {
        Assert.Throws<ArgumentException>(() =>
            new Branch(
                new BranchCode("IND01"),
                "Indore Main",
                "Central",
                new TimeOnly(18, 0),
                new TimeOnly(9, 0)));
    }

    [Fact]
    public void Deactivate_ShouldMakeBranchInactive()
    {
        var branch = CreateBranch();

        branch.Deactivate();

        Assert.False(branch.IsActive);
    }

    [Fact]
    public void IsOpenAt_ShouldReturnFalseWhenBranchIsInactive()
    {
        var branch = CreateBranch();
        branch.Deactivate();

        Assert.False(branch.IsOpenAt(new TimeOnly(10, 0)));
    }

    [Fact]
    public void IsOpenAt_ShouldReturnFalseAtClosingTime()
    {
        var branch = CreateBranch();

        Assert.False(branch.IsOpenAt(new TimeOnly(18, 0)));
    }


    [Fact]
    public void Activate_ShouldReactivateBranch()
    {
        var branch = CreateBranch();
        branch.Deactivate();

        branch.Activate();

        Assert.True(branch.IsActive);
    }


    private static Branch CreateBranch() =>
        new(
            new BranchCode("IND01"),
            "Indore Main",
            "Central",
            new TimeOnly(9, 0),
            new TimeOnly(18, 0));
}
