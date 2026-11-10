using System;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

/// <summary>
/// Tests for IdentityVerification entity
/// </summary>
public class IdentityVerificationEntityTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesVerification()
    {
        // Act
        var iv = new IdentityVerification(
            "IV-001",
            "Passport",
            "XXXX1234",
            true,
            DateTimeOffset.UtcNow.AddYears(5));

        // Assert
        Assert.Equal("IV-001", iv.Id);
        Assert.Equal("Passport", iv.DocumentType);
        Assert.Equal("XXXX1234", iv.MaskedDocumentNumber);
        Assert.True(iv.IsVerified);
    }

    [Fact]
    public void Constructor_EmptyId_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IdentityVerification(
            "", "Passport", "XXXX1234", true, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_EmptyDocumentType_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IdentityVerification(
            "IV-001", "", "XXXX1234", true, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_InvalidMaskedNumber_ShouldReject()
    {
        // Act & Assert - must be 8 chars starting with XXXX
        Assert.Throws<ArgumentException>(() => new IdentityVerification(
            "IV-001", "Passport", "XXX1234", true, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_ValidMaskedNumber_ShouldAccept()
    {
        // Act
        var iv = new IdentityVerification(
            "IV-001",
            "Passport",
            "XXXX1234",
            true,
            DateTimeOffset.UtcNow.AddYears(5));

        // Assert
        Assert.Equal("XXXX1234", iv.MaskedDocumentNumber);
    }

    [Fact]
    public void Constructor_MaskedNumberNotStartingWithXXXX_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IdentityVerification(
            "IV-001", "Passport", "YYYX1234", true, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_EmptyMaskedNumber_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IdentityVerification(
            "IV-001", "Passport", "", true, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ReferenceEquality_SameInstance_ShouldBeEqual()
    {
        // Arrange
        var iv = new IdentityVerification(
            "IV-001", "Passport", "XXXX1234", true, DateTimeOffset.UtcNow.AddYears(5));

        // Act & Assert
        Assert.Same(iv, iv);
    }

    [Fact]
    public void ReferenceEquality_DifferentInstances_ShouldNotBeEqual()
    {
        // Arrange
        var iv1 = new IdentityVerification(
            "IV-001", "Passport", "XXXX1234", true, DateTimeOffset.UtcNow.AddYears(5));
        var iv2 = new IdentityVerification(
            "IV-001", "Passport", "XXXX1234", true, DateTimeOffset.UtcNow.AddYears(5));

        // Act & Assert
        Assert.NotSame(iv1, iv2);
        Assert.NotEqual(iv1, iv2);
    }
}