using ApexPost.Application.Reports;

namespace ApexPost.Application.Services.Reports;

public sealed record BranchDashboardResult(
    string BranchCode,
    DateOnly Date,
    DailyBranchSalesReport DailySales,
    IReadOnlyList<LowStockReportItem> LowStockItems);
