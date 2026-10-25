
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public sealed class MoneyOrderPolicyTests
{
    [Fact]
    public void Constructor_NonPositiveThreshold_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MoneyOrderPolicy(new Money(0m)));
    }

    [Fact]
    public void RequiresIdentityVerification_AmountAboveThreshold_ReturnsTrue()
    {
        var policy = new MoneyOrderPolicy(new Money(10_000m));
        var moneyOrder = CreateMoneyOrder(10_001m);

        Assert.True(policy.RequiresIdentityVerification(moneyOrder));
    }

    [Fact]
    public void RequiresIdentityVerification_AmountEqualToThreshold_ReturnsFalse()
    {
        var policy = new MoneyOrderPolicy(new Money(10_000m));
        var moneyOrder = CreateMoneyOrder(10_000m);

        Assert.False(policy.RequiresIdentityVerification(moneyOrder));
    }

    [Fact]
    public void CanIssue_HighValueOrderWithSuccessfulVerification_ReturnsTrue()
    {
        var policy = new MoneyOrderPolicy(new Money(10_000m));
        var moneyOrder = CreateMoneyOrder(15_000m);
        var verification = CreateVerification(true);

        Assert.True(policy.CanIssue(moneyOrder, verification));
    }

    [Fact]
    public void CanIssue_HighValueOrderWithFailedVerification_ReturnsFalse()
    {
        var policy = new MoneyOrderPolicy(new Money(10_000m));
        var moneyOrder = CreateMoneyOrder(15_000m);
        var verification = CreateVerification(false);

        Assert.False(policy.CanIssue(moneyOrder, verification));
    }

    [Fact]
    public void CanIssue_HighValueOrderWithoutVerification_ReturnsFalse()
    {
        var policy = new MoneyOrderPolicy(new Money(10_000m));
        var moneyOrder = CreateMoneyOrder(15_000m);

        Assert.False(policy.CanIssue(moneyOrder, null));
    }

    [Fact]
    public void CanIssue_LowValueOrderWithoutVerification_ReturnsTrue()
    {
        var policy = new MoneyOrderPolicy(new Money(10_000m));
        var moneyOrder = CreateMoneyOrder(5_000m);

        Assert.True(policy.CanIssue(moneyOrder, null));
    }

    private static MoneyOrder CreateMoneyOrder(decimal amount) =>
        new(
            "MO001",
            new Money(amount),
            "Amit Sharma",
            "Priya Verma",
            new Money(25m));

    private static IdentityVerification CreateVerification(bool isVerified) =>
        new(
            "IV001",
            "Passport",
            "XXXX1234",
            isVerified,
            DateTimeOffset.UtcNow);
}
