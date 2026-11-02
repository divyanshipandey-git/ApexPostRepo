
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
        var report = (await _dataSource.GetDailyBranchSalesAsync(date, normalizedBranch, cancellationToken))
            .SingleOrDefault();
        return report is null
            ? new DailyBranchSalesReport(normalizedBranch, date, 0, 0m, 0m, 0m)
            : ToDailyReport(report);
    }

    public async Task<IReadOnlyList<DailyBranchSalesReport>> GetDailyBranchSalesReportsAsync(
        DateOnly date,
        string? branchCode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedBranch = string.IsNullOrWhiteSpace(branchCode) ? null : NormalizeBranch(branchCode);
        return (await _dataSource.GetDailyBranchSalesAsync(date, normalizedBranch, cancellationToken))
            .Select(ToDailyReport)
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

        return (await _dataSource.GetLowStockAsync(normalizedBranch, cancellationToken))
            .OrderBy(item => item.BranchCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Sku, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DailyBranchSalesReport ToDailyReport(ReportDailyBranchSales report) => new(
        NormalizeBranch(report.BranchCode), report.Date, report.TransactionCount,
        report.RetailSales, report.ServiceSales, report.RetailSales + report.ServiceSales);

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

        // SQL persistence uses a request-scoped DbContext. Keep these reads
        // sequential because EF Core does not support concurrent operations on it.
        var moneyOrders = (await _dataSource.GetMoneyOrdersAsync(cancellationToken))
            .Where(item => item.CreatedAt >= start && item.CreatedAt < end &&
                (normalizedBranch is null || string.Equals(
                    item.BranchCode,
                    normalizedBranch,
                    StringComparison.OrdinalIgnoreCase)))
            .GroupBy(item => NormalizeBranch(item.BranchCode))
            .ToDictionary(group => group.Key, group => group.Count());

        var verifications = (await _dataSource.GetIdentityVerificationsAsync(cancellationToken))
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
