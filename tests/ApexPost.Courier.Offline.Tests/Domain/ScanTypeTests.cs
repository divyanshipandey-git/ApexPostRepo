using System;
using ApexPost.Courier.Offline.Domain;
using Xunit;

namespace ApexPost.Courier.Offline.Tests.Domain;

/// <summary>
/// Tests for ScanType enum
/// </summary>
public class ScanTypeTests
{
    [Fact]
    public void ToString_Pickup_ReturnsPickup()
    {
        Assert.Equal("Pickup", ScanType.Pickup.ToString());
    }

    [Fact]
    public void ToString_InTransit_ReturnsInTransit()
    {
        Assert.Equal("InTransit", ScanType.InTransit.ToString());
    }

    [Fact]
    public void ToString_Delivered_ReturnsDelivered()
    {
        Assert.Equal("Delivered", ScanType.Delivered.ToString());
    }

    [Fact]
    public void ToString_Failed_ReturnsFailed()
    {
        Assert.Equal("Failed", ScanType.Failed.ToString());
    }

    [Fact]
    public void Parse_ValidString_ReturnsEnum()
    {
        Assert.Equal(ScanType.Pickup, Enum.Parse<ScanType>("Pickup"));
        Assert.Equal(ScanType.InTransit, Enum.Parse<ScanType>("InTransit"));
        Assert.Equal(ScanType.Delivered, Enum.Parse<ScanType>("Delivered"));
        Assert.Equal(ScanType.Failed, Enum.Parse<ScanType>("Failed"));
    }

    [Fact]
    public void Parse_CaseInsensitive_ReturnsEnum()
    {
        Assert.Equal(ScanType.Pickup, Enum.Parse<ScanType>("pickup", true));
        Assert.Equal(ScanType.InTransit, Enum.Parse<ScanType>("intransit", true));
    }

    [Fact]
    public void Parse_InvalidString_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => Enum.Parse<ScanType>("Invalid"));
    }

    [Fact]
    public void IsDefined_ValidValues_ShouldReturnTrue()
    {
        Assert.True(Enum.IsDefined(typeof(ScanType), ScanType.Pickup));
        Assert.True(Enum.IsDefined(typeof(ScanType), ScanType.InTransit));
        Assert.True(Enum.IsDefined(typeof(ScanType), ScanType.Delivered));
        Assert.True(Enum.IsDefined(typeof(ScanType), ScanType.Failed));
    }

    [Fact]
    public void GetValues_ShouldReturnFourValues()
    {
        var values = Enum.GetValues<ScanType>();
        Assert.Equal(4, values.Length);
    }

    [Fact]
    public void UnderlyingType_IsInt()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(ScanType)));
    }

    [Theory]
    [InlineData(ScanType.Pickup, 0)]
    [InlineData(ScanType.InTransit, 1)]
    [InlineData(ScanType.Delivered, 2)]
    [InlineData(ScanType.Failed, 3)]
    public void Values_HaveExpectedOrdinals(ScanType scanType, int expectedOrdinal)
    {
        Assert.Equal(expectedOrdinal, (int)scanType);
    }
}