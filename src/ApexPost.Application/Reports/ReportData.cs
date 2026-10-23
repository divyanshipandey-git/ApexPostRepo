
namespace ApexPost.Application.Reports;

public enum ReportSaleCategory
{
    Retail,
    Service
}

public sealed record ReportTransaction(
    string BranchCode,
    DateTimeOffset OccurredAt,
    ReportSaleCategory Category,
    decimal Total,
    bool IsCompleted = true);

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
    DateTimeOffset CreatedAt);

public sealed record ReportIdentityVerification(
    string BranchCode,
    string VerificationId,
    DateTimeOffset CreatedAt);
