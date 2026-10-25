using ApexPost.Application.Reports;

namespace ApexPost.Web.Models.Reports;

public sealed record ReportsIndexViewModel(
    DateOnly Date,
    string? BranchScope,
    IReadOnlyList<DailyBranchSalesReport> DailySales,
    IReadOnlyList<LowStockReportItem> LowStock,
    IReadOnlyList<ShippingVolumeReportItem> ShippingVolume,
    IReadOnlyList<BranchServiceActivityReport> ServiceActivity,
    bool CanViewAuditorReport);
