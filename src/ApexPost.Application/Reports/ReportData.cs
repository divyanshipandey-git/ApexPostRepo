
namespace ApexPost.Application.Reports;

public enum ReportSaleCategory
{
    Retail,
    Service
}

public sealed record ReportTransaction(
    string TransactionId,
    string BranchCode,
    DateTimeOffset OccurredAt,
    ReportSaleCategory Category,
    decimal Total,
    bool IsCompleted = true);

public sealed record ReportDailyBranchSales(
    string BranchCode,
    DateOnly Date,
    int TransactionCount,
    decimal RetailSales,
    decimal ServiceSales);

public sealed record ReportInventoryItem(
    string BranchCode,
    string Sku,
    string ProductName,
    int QuantityOnHand,
    int ReorderLevel);

public sealed record ReportShipment(
    string BranchCode,
    string DestinationZone,
    string ServiceLevel,
    DateTimeOffset CreatedAt);

public sealed record ReportMoneyOrder(
    string BranchCode,
    string MoneyOrderId,
    DateTimeOffset CreatedAt,
    decimal Amount = 0m,
    decimal Fee = 0m,
    string? TransactionId = null);

public sealed record ReportIdentityVerification(
    string BranchCode,
    string VerificationId,
    DateTimeOffset CreatedAt,
    string MoneyOrderId = "",
    string DocumentType = "",
    string MaskedDocumentNumber = "",
    bool IsVerified = false);

public sealed record TopProductRevenueReportItem(
    string Region,
    int RegionRank,
    string Sku,
    string ProductName,
    decimal Revenue);

public sealed record PagedReport<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
