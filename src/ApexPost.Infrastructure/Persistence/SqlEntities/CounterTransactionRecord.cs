using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Infrastructure.Persistence.SqlEntities;

public sealed class CounterTransactionRecord
{
    public string Id { get; set; } = string.Empty;
    public BranchCode BranchCode { get; set; } = new("X");
    public string CounterId { get; set; } = string.Empty;
    public string ClerkId { get; set; } = string.Empty;
    public TransactionStatus Status { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? OccurredAtUtc { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<TransactionLineRecord> Lines { get; set; } = [];
    public List<TransactionTaxRateRecord> TaxRates { get; set; } = [];
    public PaymentRecord? Payment { get; set; }
}

public sealed class TransactionLineRecord
{
    public long Id { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public TaxCategory TaxCategory { get; set; }
    public string? ReferenceId { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class TransactionTaxRateRecord
{
    public string TransactionId { get; set; } = string.Empty;
    public TaxCategory Category { get; set; }
    public decimal Rate { get; set; }
}

public sealed class PaymentRecord
{
    public string TransactionId { get; set; } = string.Empty;
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
}
