using System;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

/// <summary>
/// Tests for MoneyOrderPolicy entity
/// </summary>
public class MoneyOrderPolicyEntityTests
{
    [Fact]
    public void Constructor_ValidThreshold_CreatesPolicy()
    {
        // Act
        var policy = new MoneyOrderPolicy(new Money(1000.00m));

        // Assert
        Assert.Equal(new Money(1000.00m), policy.VerificationThreshold);
    }

    [Fact]
    public void Constructor_NullThreshold_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MoneyOrderPolicy(null!));
    }

    [Fact]
    public void Constructor_ZeroThreshold_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoneyOrderPolicy(new Money(0m)));
    }

    [Fact]
    public void Constructor_NegativeThreshold_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoneyOrderPolicy(new Money(-100m)));
    }

    [Fact]
    public void RequiresIdentityVerification_AboveThreshold_ReturnsTrue()
    {
        // Arrange
        var policy = new MoneyOrderPolicy(new Money(500.00m));
        var moneyOrder = new MoneyOrder("MO-001", new Money(1000.00m), "John", "Jane", new Money(10.00m));

        // Act
        var result = policy.RequiresIdentityVerification(moneyOrder);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RequiresIdentityVerification_BelowThreshold_ReturnsFalse()
    {
        // Arrange
        var policy = new MoneyOrderPolicy(new Money(1000.00m));
        var moneyOrder = new MoneyOrder("MO-001", new Money(500.00m), "John", "Jane", new Money(10.00m));

        // Act
        var result = policy.RequiresIdentityVerification(moneyOrder);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RequiresIdentityVerification_EqualToThreshold_ReturnsFalse()
    {
        // Arrange
        var policy = new MoneyOrderPolicy(new Money(1000.00m));
        var moneyOrder = new MoneyOrder("MO-001", new Money(1000.00m), "John", "Jane", new Money(10.00m));

        // Act
        var result = policy.RequiresIdentityVerification(moneyOrder);

        // Assert
        Assert.False(result); // Not greater than threshold
    }

    [Fact]
    public void CanIssue_BelowThreshold_ReturnsTrue()
    {
        // Arrange
        var policy = new MoneyOrderPolicy(new Money(1000.00m));
        var moneyOrder = new MoneyOrder("MO-001", new Money(500.00m), "John", "Jane", new Money(10.00m));

        // Act
        var result = policy.CanIssue(moneyOrder, null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanIssue_AboveThresholdWithoutVerification_ReturnsFalse()
    {
        // Arrange
        var policy = new MoneyOrderPolicy(new Money(500.00m));
        var moneyOrder = new MoneyOrder("MO-001", new Money(1000.00m), "John", "Jane", new Money(10.00m));

        // Act
        var result = policy.CanIssue(moneyOrder, null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanIssue_AboveThresholdWithVerifiedVerification_ReturnsTrue()
    {
        // Arrange
        var policy = new MoneyOrderPolicy(new Money(500.00m));
        var moneyOrder = new MoneyOrder("MO-001", new Money(1000.00m), "John", "Jane", new Money(10.00m));
        var verification = new IdentityVerification(
            "IV-001", "Passport", "XXXX1234", true, DateTimeOffset.UtcNow.AddYears(5));

        // Act
        var result = policy.CanIssue(moneyOrder, verification);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanIssue_AboveThresholdWithUnverifiedVerification_ReturnsFalse()
    {
        // Arrange
        var policy = new MoneyOrderPolicy(new Money(500.00m));
        var moneyOrder = new MoneyOrder("MO-001", new Money(1000.00m), "John", "Jane", new Money(10.00m));
        var verification = new IdentityVerification(
            "IV-001", "Passport", "XXXX1234", false, DateTimeOffset.UtcNow.AddYears(5));

        // Act
        var result = policy.CanIssue(moneyOrder, verification);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanIssue_NullMoneyOrder_ShouldReject()
    {
        // Arrange
        var policy = new MoneyOrderPolicy(new Money(1000.00m));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => policy.CanIssue(null!, null));
    }

    [Fact]
    public void RequiresIdentityVerification_NullMoneyOrder_ShouldReject()
    {
        // Arrange
        var policy = new MoneyOrderPolicy(new Money(1000.00m));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => policy.RequiresIdentityVerification(null!));
    }
}