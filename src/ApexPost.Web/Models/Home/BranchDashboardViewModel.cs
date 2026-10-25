using ApexPost.Application.Reports;

namespace ApexPost.Web.Models.Home;

public sealed record BranchDashboardViewModel(
    string BranchCode,
    DateOnly Date,
    int TransactionCount,
    decimal RetailSales,
    decimal ServiceSales,
    decimal TotalSales,
    IReadOnlyList<LowStockReportItem> LowStockItems);
