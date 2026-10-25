namespace ApexPost.Web.Models.Transactions;

public sealed record TransactionSummaryViewModel(
    string TransactionId,
    string BranchCode,
    string Status,
    decimal Total);
