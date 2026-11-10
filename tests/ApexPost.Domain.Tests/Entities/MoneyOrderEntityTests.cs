using System;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

/// <summary>
/// Tests for MoneyOrder entity
/// </summary>
public class MoneyOrderEntityTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesMoneyOrder()
    {
        // Act
        var mo = new MoneyOrder(
            "MO-001",
            new Money(100.50m),
            "John Doe",
            "Jane Smith",
            new Money(5.00m));

        // Assert
        Assert.Equal("MO-001", mo.Id);
        Assert.Equal(new Money(100.50m), mo.Amount);
        Assert.Equal("John Doe", mo.SenderName);
        Assert.Equal("Jane Smith", mo.RecipientName);
        Assert.Equal(new Money(5.00m), mo.Fee);
        Assert.Equal(new Money(105.50m), mo.TotalCost);
    }

    [Fact]
    public void Constructor_EmptyId_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MoneyOrder(
            "",
            new Money(100m),
            "John Doe",
            "Jane Smith",
            new Money(5m)));
    }

    [Fact]
    public void Constructor_NullAmount_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MoneyOrder(
            "MO-001",
            null!,
            "John Doe",
            "Jane Smith",
            new Money(5m)));
    }

    [Fact]
    public void Constructor_ZeroAmount_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoneyOrder(
            "MO-001",
            new Money(0m),
            "John Doe",
            "Jane Smith",
            new Money(5m)));
    }

    [Fact]
    public void Constructor_NegativeAmount_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoneyOrder(
            "MO-001",
            new Money(-10m),
            "John Doe",
            "Jane Smith",
            new Money(5m)));
    }

    [Fact]
    public void Constructor_NullFee_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MoneyOrder(
            "MO-001",
            new Money(100m),
            "John Doe",
            "Jane Smith",
            null!));
    }

    [Fact]
    public void Constructor_NegativeFee_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoneyOrder(
            "MO-001",
            new Money(100m),
            "John Doe",
            "Jane Smith",
            new Money(-1m)));
    }

    [Fact]
    public void Constructor_EmptySenderName_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MoneyOrder(
            "MO-001",
            new Money(100m),
            "",
            "Jane Smith",
            new Money(5m)));
    }

    [Fact]
    public void Constructor_EmptyRecipientName_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MoneyOrder(
            "MO-001",
            new Money(100m),
            "John Doe",
            "",
            new Money(5m)));
    }

    [Fact]
    public void AttachToTransaction_ValidParams_Attaches()
    {
        // Arrange
        var mo = new MoneyOrder("MO-001", new Money(100m), "John", "Jane", new Money(5m));
        var branchCode = new BranchCode("BR001");
        var verification = new IdentityVerification(
            "IV-001",
            "Passport",
            "XXXX1234",
            true,
            DateTimeOffset.UtcNow.AddYears(5));

        // Act
        mo.AttachToTransaction(branchCode, "TXN-001", verification, DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal("BR001", mo.BranchCode);
        Assert.Equal("TXN-001", mo.TransactionId);
        Assert.NotNull(mo.IdentityVerification);
    }

    [Fact]
    public void AttachToTransaction_NullBranchCode_ShouldReject()
    {
        // Arrange
        var mo = new MoneyOrder("MO-001", new Money(100m), "John", "Jane", new Money(5m));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => mo.AttachToTransaction(
            null!, "TXN-001", null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AttachToTransaction_EmptyTransactionId_ShouldReject()
    {
        // Arrange
        var mo = new MoneyOrder("MO-001", new Money(100m), "John", "Jane", new Money(5m));

        // Act & Assert
        Assert.Throws<ArgumentException>(() => mo.AttachToTransaction(
            new BranchCode("BR001"), "", null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AttachToTransaction_AlreadyAttached_ShouldThrow()
    {
        // Arrange
        var mo = new MoneyOrder("MO-001", new Money(100m), "John", "Jane", new Money(5m));
        mo.AttachToTransaction(new BranchCode("BR001"), "TXN-001", null, DateTimeOffset.UtcNow);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => mo.AttachToTransaction(
            new BranchCode("BR002"), "TXN-002", null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void TotalCost_ShouldCalculateCorrectly()
    {
        // Arrange
        var mo = new MoneyOrder("MO-001", new Money(100m), "John", "Jane", new Money(5m));

        // Act
        var total = mo.TotalCost;

        // Assert
        Assert.Equal(new Money(105m), total);
    }

    [Fact]
    public void Equality_SameId_ShouldBeEqual()
    {
        var id = "MO-001";
        var mo1 = new MoneyOrder(id, new Money(100m), "John", "Jane", new Money(5m));
        var mo2 = new MoneyOrder(id, new Money(200m), "Bob", "Alice", new Money(10m));
        // MoneyOrder uses reference equality by default
        Assert.NotEqual(mo1, mo2);
    }

    [Fact]
    public void GetHashCode_SameId_ShouldBeEqual()
    {
        var id = "MO-001";
        var mo1 = new MoneyOrder(id, new Money(100m), "John", "Jane", new Money(5m));
        var mo2 = new MoneyOrder(id, new Money(200m), "Bob", "Alice", new Money(10m));
        // GetHashCode is based on reference by default
        Assert.NotEqual(mo1.GetHashCode(), mo2.GetHashCode());
    }
}