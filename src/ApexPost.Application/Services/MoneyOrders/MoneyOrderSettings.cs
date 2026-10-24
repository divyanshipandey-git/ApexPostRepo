using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services.MoneyOrders;

public sealed record MoneyOrderSettings
{
    public MoneyOrderSettings(Money fee, Money verificationThreshold)
    {
        ArgumentNullException.ThrowIfNull(fee);
        ArgumentNullException.ThrowIfNull(verificationThreshold);
        if (fee.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fee));
        }
        if (verificationThreshold.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(verificationThreshold));
        }

        Fee = fee;
        VerificationThreshold = verificationThreshold;
    }

    public Money Fee { get; }
    public Money VerificationThreshold { get; }
}
