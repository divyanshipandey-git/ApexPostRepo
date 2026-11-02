
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
        CancellationToken cancellationToken = default,
        int threshold = 5)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(threshold, 100_000);

        var normalizedBranch = string.IsNullOrWhiteSpace(branchCode)
            ? null
            : NormalizeBranch(branchCode);

        return (await _dataSource.GetLowStockAsync(normalizedBranch, threshold, cancellationToken))
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

        return (await _dataSource.GetShippingVolumeAsync(date, normalizedBranch, cancellationToken))
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

        var normalizedBranch = string.IsNullOrWhiteSpace(branchCode) ? null : NormalizeBranch(branchCode);
        return await _dataSource.GetBranchServiceActivityAsync(date, normalizedBranch, cancellationToken);
    }

    private static string NormalizeBranch(string branchCode) =>
        branchCode.Trim().ToUpperInvariant();
}
