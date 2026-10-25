
using ApexPost.Application.Abstractions.Reports;
using ApexPost.Application.Reports;

namespace ApexPost.Application.Services.Reports;

public sealed class ReportingService
{
    private readonly IReportDataSource _dataSource;

    public ReportingService(IReportDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
    }

    public async Task<DailyBranchSalesReport> GetDailyBranchSalesAsync(
        string branchCode,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedBranch = NormalizeBranch(branchCode);
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var transactions = await _dataSource.GetTransactionsAsync(cancellationToken);

        var completed = transactions
            .Where(t =>
                string.Equals(t.BranchCode, normalizedBranch, StringComparison.OrdinalIgnoreCase)
                && t.IsCompleted
                && t.OccurredAt >= start
                && t.OccurredAt < end)
            .ToArray();

        var retail = completed
            .Where(t => t.Category == ReportSaleCategory.Retail)
            .Sum(t => t.Total);

        var services = completed
            .Where(t => t.Category == ReportSaleCategory.Service)
            .Sum(t => t.Total);

        return new DailyBranchSalesReport(
            normalizedBranch,
            date,
            completed.Select(transaction => transaction.TransactionId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            retail,
            services,
            retail + services);
    }

    public async Task<IReadOnlyList<DailyBranchSalesReport>> GetDailyBranchSalesReportsAsync(
        DateOnly date,
        string? branchCode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedBranch = string.IsNullOrWhiteSpace(branchCode) ? null : NormalizeBranch(branchCode);
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var transactions = await _dataSource.GetTransactionsAsync(cancellationToken);

        return transactions
            .Where(transaction => transaction.IsCompleted &&
                transaction.OccurredAt >= start && transaction.OccurredAt < end &&
                (normalizedBranch is null || string.Equals(
                    transaction.BranchCode,
                    normalizedBranch,
                    StringComparison.OrdinalIgnoreCase)))
            .GroupBy(transaction => NormalizeBranch(transaction.BranchCode), StringComparer.OrdinalIgnoreCase)
            .Select(group => new DailyBranchSalesReport(
                group.Key,
                date,
                group.Select(transaction => transaction.TransactionId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                group.Where(transaction => transaction.Category == ReportSaleCategory.Retail)
                    .Sum(transaction => transaction.Total),
                group.Where(transaction => transaction.Category == ReportSaleCategory.Service)
                    .Sum(transaction => transaction.Total),
                group.Sum(transaction => transaction.Total)))
            .OrderBy(report => report.BranchCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<LowStockReportItem>> GetLowStockReportAsync(
        string? branchCode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedBranch = string.IsNullOrWhiteSpace(branchCode)
            ? null
            : NormalizeBranch(branchCode);

        var inventory = await _dataSource.GetInventoryAsync(cancellationToken);

        return inventory
            .Where(item =>
                item.QuantityOnHand <= item.ReorderLevel
                && (normalizedBranch is null
                    || string.Equals(
                        item.BranchCode,
                        normalizedBranch,
                        StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.BranchCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(item => new LowStockReportItem(
                NormalizeBranch(item.BranchCode),
                item.Sku,
                item.ProductName,
                item.QuantityOnHand,
                item.ReorderLevel))
            .ToArray();
    }

    public async Task<IReadOnlyList<ShippingVolumeReportItem>> GetShippingVolumeReportAsync(
        DateOnly date,
        string? branchCode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedBranch = string.IsNullOrWhiteSpace(branchCode)
            ? null
            : NormalizeBranch(branchCode);

        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var shipments = await _dataSource.GetShipmentsAsync(cancellationToken);

        return shipments
            .Where(shipment =>
                shipment.CreatedAt >= start
                && shipment.CreatedAt < end
                && (normalizedBranch is null
                    || string.Equals(
                        shipment.BranchCode,
                        normalizedBranch,
                        StringComparison.OrdinalIgnoreCase)))
            .GroupBy(shipment => new
            {
                Zone = shipment.DestinationZone.Trim().ToUpperInvariant(),
                Level = shipment.ServiceLevel.Trim()
            })
            .Select(group => new ShippingVolumeReportItem(
                group.Key.Zone,
                group.Key.Level,
                group.Count()))
            .OrderBy(item => item.DestinationZone, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ServiceLevel, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<BranchServiceActivityReport>>
        GetBranchServiceActivityReportAsync(
            DateOnly date,
            string? branchCode = null,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var normalizedBranch = string.IsNullOrWhiteSpace(branchCode) ? null : NormalizeBranch(branchCode);

        var moneyOrdersTask = _dataSource.GetMoneyOrdersAsync(cancellationToken);
        var verificationsTask = _dataSource.GetIdentityVerificationsAsync(cancellationToken);

        await Task.WhenAll(moneyOrdersTask, verificationsTask);

        var moneyOrders = (await moneyOrdersTask)
            .Where(item => item.CreatedAt >= start && item.CreatedAt < end &&
                (normalizedBranch is null || string.Equals(
                    item.BranchCode,
                    normalizedBranch,
                    StringComparison.OrdinalIgnoreCase)))
            .GroupBy(item => NormalizeBranch(item.BranchCode))
            .ToDictionary(group => group.Key, group => group.Count());

        var verifications = (await verificationsTask)
            .Where(item => item.CreatedAt >= start && item.CreatedAt < end &&
                (normalizedBranch is null || string.Equals(
                    item.BranchCode,
                    normalizedBranch,
                    StringComparison.OrdinalIgnoreCase)))
            .GroupBy(item => NormalizeBranch(item.BranchCode))
            .ToDictionary(group => group.Key, group => group.Count());

        return moneyOrders.Keys
            .Union(verifications.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .Select(code => new BranchServiceActivityReport(
                code,
                moneyOrders.GetValueOrDefault(code),
                verifications.GetValueOrDefault(code)))
            .ToArray();
    }

    private static string NormalizeBranch(string branchCode) =>
        branchCode.Trim().ToUpperInvariant();
}
