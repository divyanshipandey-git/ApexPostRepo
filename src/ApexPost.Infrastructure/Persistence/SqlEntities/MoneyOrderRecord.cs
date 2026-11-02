using ApexPost.Domain.ValueObjects;

namespace ApexPost.Infrastructure.Persistence.SqlEntities;

public sealed class MoneyOrderRecord
{
    public string Id { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public BranchCode? BranchCode { get; set; }
    public string? TransactionId { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public IdentityVerificationRecord? IdentityVerification { get; set; }
}

public sealed class IdentityVerificationRecord
{
    public string Id { get; set; } = string.Empty;
    public string MoneyOrderId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string MaskedDocumentNumber { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public DateTimeOffset VerifiedAtUtc { get; set; }
}
