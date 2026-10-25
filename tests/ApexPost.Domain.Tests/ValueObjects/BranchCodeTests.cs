
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.ValueObjects;

public class BranchCodeTests
{
    [Fact]
    public void Constructor_ShouldNormalizeBranchCode()
    {
        var branchCode = new BranchCode(" bh01 ");

        Assert.Equal("BH01", branchCode.Value);
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyBranchCode()
    {
        Assert.Throws<ArgumentException>(
            () => new BranchCode(" "));
    }

    [Fact]
    public void Constructor_ShouldRejectSpecialCharacters()
    {
        Assert.Throws<ArgumentException>(
            () => new BranchCode("BH-01"));
    }

    [Fact]
    public void Constructor_ShouldRejectCodeLongerThanTenCharacters()
    {
        Assert.Throws<ArgumentException>(
            () => new BranchCode("BRANCHCODE1"));
    }
}
