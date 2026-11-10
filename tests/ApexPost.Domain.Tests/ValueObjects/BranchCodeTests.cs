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
        Assert.Throws<ArgumentException>(() => new BranchCode(" "));
    }

    [Fact]
    public void Constructor_ShouldRejectSpecialCharacters()
    {
        Assert.Throws<ArgumentException>(() => new BranchCode("BH@01"));
    }

    [Fact]
    public void Constructor_ShouldRejectCodeLongerThanTenCharacters()
    {
        Assert.Throws<ArgumentException>(() => new BranchCode("BRANCHCODE1"));
    }

    [Fact]
    public void Equality_SameValue_ShouldBeEqual()
    {
        var bc1 = new BranchCode("BR001");
        var bc2 = new BranchCode("BR001");
        Assert.Equal(bc1, bc2);
        Assert.True(bc1 == bc2);
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        var bc1 = new BranchCode("BR001");
        var bc2 = new BranchCode("BR001");
        Assert.Equal(bc1.GetHashCode(), bc2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var bc = new BranchCode("BR001");
        Assert.Equal("BR001", bc.ToString());
    }

    [Theory]
    [InlineData("BR001")]
    [InlineData("BR999")]
    [InlineData("NY001")]
    [InlineData("LA042")]
    [InlineData("A")]
    [InlineData("BRANCH01")]
    public void Constructor_ValidFormats_ShouldAccept(string code)
    {
        var bc = new BranchCode(code);
        Assert.Equal(code.ToUpperInvariant().Trim(), bc.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("B@001")]
    [InlineData("BRANCHCODE11")] // 12 chars, exceeds 10
    public void Constructor_InvalidFormats_ShouldReject(string code)
    {
        Assert.Throws<ArgumentException>(() => new BranchCode(code));
    }
}