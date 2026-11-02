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
