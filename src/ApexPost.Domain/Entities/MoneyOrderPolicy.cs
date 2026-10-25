
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class MoneyOrderPolicy
{
    public Money VerificationThreshold { get; }

    public MoneyOrderPolicy(Money verificationThreshold)
    {
        ArgumentNullException.ThrowIfNull(verificationThreshold);

        if (verificationThreshold.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(verificationThreshold),
                "Verification threshold must be greater than zero.");
        }

        VerificationThreshold = verificationThreshold;
    }

    public bool RequiresIdentityVerification(MoneyOrder moneyOrder)
    {
        ArgumentNullException.ThrowIfNull(moneyOrder);

        return moneyOrder.Amount.Amount > VerificationThreshold.Amount;
    }

    public bool CanIssue(
        MoneyOrder moneyOrder,
        IdentityVerification? verification)
    {
        ArgumentNullException.ThrowIfNull(moneyOrder);

        if (!RequiresIdentityVerification(moneyOrder))
        {
            return true;
        }

        return verification is { IsVerified: true };
    }
}
