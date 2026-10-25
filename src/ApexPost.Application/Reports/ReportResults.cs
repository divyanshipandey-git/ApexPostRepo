
namespace ApexPost.Application.Reports;

public sealed record DailyBranchSalesReport(
    string BranchCode,
    DateOnly Date,
    int TransactionCount,
    decimal RetailSales,
    decimal ServiceSales,
    decimal TotalSales);

public sealed record LowStockReportItem(
    string BranchCode,
    string Sku,
    string ProductName,
    int QuantityOnHand,
    int ReorderLevel);

public sealed record ShippingVolumeReportItem(
    string DestinationZone,
    string ServiceLevel,
    int ShipmentCount);

public sealed record BranchServiceActivityReport(
    string BranchCode,
    int MoneyOrderCount,
    int IdentityVerificationCount);
