
using System.Text.Json.Serialization;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class MoneyOrder
{
    public string Id { get; }
    public Money Amount { get; }
    public string SenderName { get; }
    public string RecipientName { get; }
    public Money Fee { get; }

    [JsonInclude]
    public string? BranchCode { get; private set; }

    [JsonInclude]
    public string? TransactionId { get; private set; }

    [JsonInclude]
    public IdentityVerification? IdentityVerification { get; private set; }

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

    public void AttachToTransaction(
        BranchCode branchCode,
        string transactionId,
        IdentityVerification? identityVerification)
    {
        ArgumentNullException.ThrowIfNull(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

        if (TransactionId is not null || BranchCode is not null)
        {
            throw new InvalidOperationException("A money order can only be attached to one counter transaction.");
        }

        BranchCode = branchCode.Value;
        TransactionId = transactionId.Trim();
        IdentityVerification = identityVerification;
    }
}
