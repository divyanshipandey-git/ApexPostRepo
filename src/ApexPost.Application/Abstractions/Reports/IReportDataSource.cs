
using ApexPost.Application.Reports;

namespace ApexPost.Application.Abstractions.Reports;

public interface IReportDataSource
{
    async Task<IReadOnlyList<ReportDailyBranchSales>> GetDailyBranchSalesAsync(
        DateOnly date,
        string? branchCode = null,
        CancellationToken cancellationToken = default)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return (await GetTransactionsAsync(cancellationToken))
            .Where(item => item.IsCompleted && item.OccurredAt >= start && item.OccurredAt < end &&
                (branchCode is null || string.Equals(item.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(item => item.BranchCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ReportDailyBranchSales(
                group.Key, date,
                group.Select(item => item.TransactionId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                group.Where(item => item.Category == ReportSaleCategory.Retail).Sum(item => item.Total),
                group.Where(item => item.Category == ReportSaleCategory.Service).Sum(item => item.Total)))
            .ToArray();
    }

    async Task<IReadOnlyList<LowStockReportItem>> GetLowStockAsync(
        string? branchCode = null,
        CancellationToken cancellationToken = default) =>
        (await GetInventoryAsync(cancellationToken))
            .Where(item => item.QuantityOnHand <= item.ReorderLevel &&
                (branchCode is null || string.Equals(item.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase)))
            .Select(item => new LowStockReportItem(item.BranchCode, item.Sku, item.ProductName, item.QuantityOnHand, item.ReorderLevel))
            .ToArray();

    Task<IReadOnlyList<ReportTransaction>> GetTransactionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportInventoryItem>> GetInventoryAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportShipment>> GetShipmentsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportMoneyOrder>> GetMoneyOrdersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportIdentityVerification>> GetIdentityVerificationsAsync(
        CancellationToken cancellationToken = default);
}
