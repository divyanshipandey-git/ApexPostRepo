namespace ApexPost.Web.Models.Transactions;

public sealed record TransactionReceiptViewModel(
    string TransactionId,
    string BranchCode,
    string CounterId,
    string Status,
    string PaymentMethod,
    IReadOnlyList<TransactionReceiptLineViewModel> Lines,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string? ShipmentId);

public sealed record TransactionReceiptLineViewModel(
    string Description,
    string ItemType,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string TaxCategory);
