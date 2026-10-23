
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class MoneyOrder
{
    public string Id { get; }
    public Money Amount { get; }
    public string SenderName { get; }
    public string RecipientName { get; }
    public Money Fee { get; }

    public MoneyOrder(
        string id,
        Money amount,
        string senderName,
        string recipientName,
        Money fee)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        ArgumentNullException.ThrowIfNull(fee);

        if (amount.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Money order amount must be greater than zero.");
        }

        if (fee.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fee),
                "Money order fee cannot be negative.");
        }

        Id = id.Trim();
        Amount = amount;
        SenderName = senderName.Trim();
        RecipientName = recipientName.Trim();
        Fee = fee;
    }

    public Money TotalCost => Amount.Add(Fee);
}
