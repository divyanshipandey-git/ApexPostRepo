
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

    async Task<IReadOnlyList<LowStockReportItem>> GetLowStockAsync(
        string? branchCode,
        int threshold,
        CancellationToken cancellationToken = default) =>
        (await GetInventoryAsync(cancellationToken))
            .Where(item => item.QuantityOnHand <= threshold &&
                (branchCode is null || string.Equals(item.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase)))
            .Select(item => new LowStockReportItem(item.BranchCode, item.Sku, item.ProductName, item.QuantityOnHand, threshold))
            .ToArray();

    async Task<IReadOnlyList<ShippingVolumeReportItem>> GetShippingVolumeAsync(
        DateOnly date,
        string? branchCode,
        CancellationToken cancellationToken = default)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return (await GetShipmentsAsync(cancellationToken))
            .Where(shipment => shipment.CreatedAt >= start && shipment.CreatedAt < end &&
                (branchCode is null || string.Equals(shipment.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(shipment => new
            {
                Zone = shipment.DestinationZone.Trim().ToUpperInvariant(),
                Level = shipment.ServiceLevel.Trim()
            })
            .Select(group => new ShippingVolumeReportItem(group.Key.Zone, group.Key.Level, group.Count()))
            .ToArray();
    }

    async Task<IReadOnlyList<BranchServiceActivityReport>> GetBranchServiceActivityAsync(
        DateOnly date,
        string? branchCode,
        CancellationToken cancellationToken = default)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var moneyOrders = (await GetMoneyOrdersAsync(cancellationToken))
            .Where(item => item.CreatedAt >= start && item.CreatedAt < end &&
                (branchCode is null || string.Equals(item.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(item => item.BranchCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count());
        var verifications = (await GetIdentityVerificationsAsync(cancellationToken))
            .Where(item => item.CreatedAt >= start && item.CreatedAt < end &&
                (branchCode is null || string.Equals(item.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(item => item.BranchCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count());

        return moneyOrders.Keys
            .Union(verifications.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .Select(code => new BranchServiceActivityReport(
                code, moneyOrders.GetValueOrDefault(code), verifications.GetValueOrDefault(code)))
            .ToArray();
    }

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

    Task<IReadOnlyList<TopProductRevenueReportItem>> GetTopProductsByRegionAsync(
        DateOnly fromDate,
        DateOnly toDate,
        string? branchCode,
        CancellationToken cancellationToken = default);

    async Task<PagedReport<ReportMoneyOrder>> GetMoneyOrderPageAsync(
        string? branchCode,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var rows = await GetMoneyOrdersAsync(cancellationToken);
        var filtered = rows
            .Where(item => branchCode is null ||
                string.Equals(item.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase))
            .Where(item => fromDate is null || DateOnly.FromDateTime(item.CreatedAt.UtcDateTime) >= fromDate)
            .Where(item => toDate is null || DateOnly.FromDateTime(item.CreatedAt.UtcDateTime) <= toDate)
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.MoneyOrderId, StringComparer.Ordinal)
            .ToArray();
        return Paginate(filtered, pageNumber, pageSize);
    }

    async Task<PagedReport<ReportIdentityVerification>> GetIdentityVerificationPageAsync(
        string? branchCode,
        DateOnly? fromDate,
        DateOnly? toDate,
        bool? isVerified,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var rows = await GetIdentityVerificationsAsync(cancellationToken);
        var filtered = rows
            .Where(item => branchCode is null ||
                string.Equals(item.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase))
            .Where(item => fromDate is null || DateOnly.FromDateTime(item.CreatedAt.UtcDateTime) >= fromDate)
            .Where(item => toDate is null || DateOnly.FromDateTime(item.CreatedAt.UtcDateTime) <= toDate)
            .Where(item => isVerified is null || item.IsVerified == isVerified)
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.VerificationId, StringComparer.Ordinal)
            .ToArray();
        return Paginate(filtered, pageNumber, pageSize);
    }

    async Task<PagedReport<ReportTransaction>> GetTransactionPageAsync(
        string? branchCode,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePageFilters(fromDate, toDate, pageNumber, pageSize);
        var rows = await GetTransactionsAsync(cancellationToken);
        var filtered = rows
            .Where(item => branchCode is null ||
                string.Equals(item.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase))
            .Where(item => fromDate is null || DateOnly.FromDateTime(item.OccurredAt.UtcDateTime) >= fromDate)
            .Where(item => toDate is null || DateOnly.FromDateTime(item.OccurredAt.UtcDateTime) <= toDate)
            .OrderByDescending(item => item.OccurredAt)
            .ThenBy(item => item.TransactionId, StringComparer.Ordinal)
            .ThenBy(item => item.Category);
        return Paginate(filtered.ToArray(), pageNumber, pageSize);
    }

    async Task<PagedReport<ReportShipment>> GetShipmentPageAsync(
        string? branchCode,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePageFilters(fromDate, toDate, pageNumber, pageSize);
        var rows = await GetShipmentsAsync(cancellationToken);
        var filtered = rows
            .Where(item => branchCode is null ||
                string.Equals(item.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase))
            .Where(item => fromDate is null || DateOnly.FromDateTime(item.CreatedAt.UtcDateTime) >= fromDate)
            .Where(item => toDate is null || DateOnly.FromDateTime(item.CreatedAt.UtcDateTime) <= toDate)
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.DestinationZone, StringComparer.Ordinal)
            .ThenBy(item => item.ServiceLevel, StringComparer.Ordinal);
        return Paginate(filtered.ToArray(), pageNumber, pageSize);
    }

    private static void ValidatePageFilters(
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageNumber,
        int pageSize)
    {
        if (fromDate is { } start && toDate is { } end && start > end)
        {
            throw new ArgumentException("The start date must be on or before the end date.");
        }
        if (toDate == DateOnly.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(toDate), "The end date must be earlier than the maximum supported date.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);
        if (pageNumber > int.MaxValue / pageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "The requested page is too large.");
        }
    }

    private static PagedReport<T> Paginate<T>(
        IReadOnlyList<T> filtered,
        int pageNumber,
        int pageSize) =>
        new(
            filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(),
            pageNumber,
            pageSize,
            filtered.Count);
}
