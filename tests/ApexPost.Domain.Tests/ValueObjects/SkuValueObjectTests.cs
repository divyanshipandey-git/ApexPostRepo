using System;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for Sku value object
/// </summary>
public class SkuValueObjectTests
{
    [Fact]
    public void Sku_ValidValues_ShouldCreate()
    {
        // Act
        var sku = new Sku("ABC123");

        // Assert
        Assert.Equal("ABC123", sku.Value);
    }

    [Fact]
    public void Sku_ShouldNormalizeWhitespaceAndCase()
    {
        // Act
        var sku = new Sku("  abc123  ");

        // Assert
        Assert.Equal("ABC123", sku.Value);
    }

    [Fact]
    public void Sku_EmptyValue_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Sku(""));
    }

    [Fact]
    public void Sku_OnlyWhitespace_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Sku("   "));
    }

    [Fact]
    public void Sku_WithSpecialCharacters_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Sku("ABC@123"));
    }

    [Fact]
    public void Sku_LongerThan20Chars_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Sku("ABCDEFGHIJKLMNOPQRSTU"));
    }

    [Fact]
    public void Sku_Equality_SameValue_ShouldBeEqual()
    {
        var s1 = new Sku("ABC123");
        var s2 = new Sku("ABC123");
        Assert.Equal(s1, s2);
        Assert.True(s1 == s2);
    }

    [Fact]
    public void Sku_GetHashCode_SameValue_ShouldBeEqual()
    {
        var s1 = new Sku("ABC123");
        var s2 = new Sku("ABC123");
        Assert.Equal(s1.GetHashCode(), s2.GetHashCode());
    }

    [Fact]
    public void Sku_ToString_ReturnsValue()
    {
        var s = new Sku("ABC123");
        Assert.Equal("ABC123", s.ToString());
    }
}