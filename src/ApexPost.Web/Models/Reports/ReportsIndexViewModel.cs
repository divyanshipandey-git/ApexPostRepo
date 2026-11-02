using ApexPost.Application.Reports;

namespace ApexPost.Web.Models.Reports;

public sealed record ReportsIndexViewModel(
    DateOnly Date,
    string? BranchScope,
    int Threshold,
    IReadOnlyList<ReportBranchOption> Branches,
    IReadOnlyList<DailyBranchSalesReport> DailySales,
    IReadOnlyList<LowStockReportItem> LowStock,
    IReadOnlyList<ShippingVolumeReportItem> ShippingVolume,
    IReadOnlyList<BranchServiceActivityReport> ServiceActivity,
    bool CanViewAuditorReport);

public sealed record ReportBranchOption(string Code, string Name);

public sealed record MoneyOrdersReportViewModel(
    PagedReport<ReportMoneyOrder> Report,
    IReadOnlyList<ReportBranchOption> Branches,
    string? BranchCode,
    DateOnly? FromDate,
    DateOnly? ToDate);

public sealed record IdentityVerificationsReportViewModel(
    PagedReport<ReportIdentityVerification> Report,
    IReadOnlyList<ReportBranchOption> Branches,
    string? BranchCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool? IsVerified);

public sealed record TopProductsReportViewModel(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<ReportBranchOption> Branches,
    string? BranchCode,
    IReadOnlyList<TopProductRevenueReportItem> Products);
