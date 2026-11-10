using System;
using ApexPost.Courier.Offline.Domain;
using Xunit;

namespace ApexPost.Courier.Offline.Tests.Domain;

/// <summary>
/// Tests for SyncStatus enum
/// </summary>
public class SyncStatusTests
{
    [Fact]
    public void ToString_Pending_ReturnsPending()
    {
        Assert.Equal("Pending", SyncStatus.Pending.ToString());
    }

    [Fact]
    public void ToString_Synced_ReturnsSynced()
    {
        Assert.Equal("Synced", SyncStatus.Synced.ToString());
    }

    [Fact]
    public void ToString_Failed_ReturnsFailed()
    {
        Assert.Equal("Failed", SyncStatus.Failed.ToString());
    }

    [Fact]
    public void Parse_ValidString_ReturnsEnum()
    {
        Assert.Equal(SyncStatus.Pending, Enum.Parse<SyncStatus>("Pending"));
        Assert.Equal(SyncStatus.Synced, Enum.Parse<SyncStatus>("Synced"));
        Assert.Equal(SyncStatus.Failed, Enum.Parse<SyncStatus>("Failed"));
    }

    [Fact]
    public void Parse_InvalidString_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => Enum.Parse<SyncStatus>("Invalid"));
    }

    [Fact]
    public void IsDefined_ValidValues_ShouldReturnTrue()
    {
        Assert.True(Enum.IsDefined(typeof(SyncStatus), SyncStatus.Pending));
        Assert.True(Enum.IsDefined(typeof(SyncStatus), SyncStatus.Synced));
        Assert.True(Enum.IsDefined(typeof(SyncStatus), SyncStatus.Failed));
    }

    [Fact]
    public void GetValues_ShouldReturnThreeValues()
    {
        var values = Enum.GetValues<SyncStatus>();
        Assert.Equal(3, values.Length);
    }

    [Fact]
    public void UnderlyingType_IsInt()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(SyncStatus)));
    }

    [Theory]
    [InlineData(SyncStatus.Pending, 0)]
    [InlineData(SyncStatus.Synced, 1)]
    [InlineData(SyncStatus.Failed, 2)]
    public void Values_HaveExpectedOrdinals(SyncStatus status, int expectedOrdinal)
    {
        Assert.Equal(expectedOrdinal, (int)status);
    }
}