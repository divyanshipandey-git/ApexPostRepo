
using ApexPost.Application.Abstractions.Reports;
using ApexPost.Application.Reports;
using ApexPost.Application.Services.Reports;
using Xunit;

namespace ApexPost.Application.Tests.Services.Reports;

public sealed class ReportingServiceTests
{
    private static readonly DateOnly ReportDate = new(2026, 10, 23);

    [Fact]
    public void Constructor_WhenDataSourceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ReportingService(null!));
    }

    [Fact]
    public async Task GetDailyBranchSalesAsync_SplitsRetailAndServiceSales()
    {
        var source = new FakeReportDataSource
        {
            Transactions =
            [
                Transaction("IND01", ReportDate, ReportSaleCategory.Retail, 100m),
                Transaction("IND01", ReportDate, ReportSaleCategory.Service, 50m),
                Transaction("IND01", ReportDate, ReportSaleCategory.Retail, 25m),
                Transaction("IND01", ReportDate, ReportSaleCategory.Retail, 999m, false),
                Transaction("BPL01", ReportDate, ReportSaleCategory.Retail, 500m)
            ]
        };

        var report = await new ReportingService(source)
            .GetDailyBranchSalesAsync(" ind01 ", ReportDate);

        Assert.Equal("IND01", report.BranchCode);
        Assert.Equal(3, report.TransactionCount);
        Assert.Equal(125m, report.RetailSales);
        Assert.Equal(50m, report.ServiceSales);
        Assert.Equal(175m, report.TotalSales);
    }

    [Fact]
    public async Task GetDailyBranchSalesAsync_ExcludesTransactionsFromOtherDates()
    {
        var source = new FakeReportDataSource
        {
            Transactions =
            [
                Transaction("IND01", ReportDate.AddDays(-1), ReportSaleCategory.Retail, 100m),
                Transaction("IND01", ReportDate.AddDays(1), ReportSaleCategory.Service, 200m)
            ]
        };

        var report = await new ReportingService(source)
            .GetDailyBranchSalesAsync("IND01", ReportDate);

        Assert.Equal(0, report.TransactionCount);
        Assert.Equal(0m, report.TotalSales);
    }

    [Fact]
    public async Task GetDailyBranchSalesAsync_WhenBranchCodeIsBlank_Throws()
    {
        var service = new ReportingService(new FakeReportDataSource());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetDailyBranchSalesAsync(" ", ReportDate));
    }

    [Fact]
    public async Task GetLowStockReportAsync_ReturnsOnlyItemsAtOrBelowReorderLevel()
    {
        var source = new FakeReportDataSource
        {
            Inventory =
            [
                new("IND01", "SKU001", "Envelope", 2, 5),
                new("IND01", "SKU002", "Box", 5, 5),
                new("IND01", "SKU003", "Tape", 10, 5),
                new("BPL01", "SKU004", "Label", 1, 3)
            ]
        };

        var report = await new ReportingService(source).GetLowStockReportAsync();

        Assert.Equal(3, report.Count);
        Assert.Contains(report, item => item.Sku == "SKU001");
        Assert.Contains(report, item => item.Sku == "SKU002");
        Assert.DoesNotContain(report, item => item.Sku == "SKU003");
    }

    [Fact]
    public async Task GetLowStockReportAsync_CanFilterByBranch()
    {
        var source = new FakeReportDataSource
        {
            Inventory =
            [
                new("IND01", "SKU001", "Envelope", 1, 5),
                new("BPL01", "SKU002", "Box", 1, 5)
            ]
        };

        var report = await new ReportingService(source)
            .GetLowStockReportAsync(" ind01 ");

        var item = Assert.Single(report);
        Assert.Equal("IND01", item.BranchCode);
        Assert.Equal("SKU001", item.Sku);
    }

    [Fact]
    public async Task GetShippingVolumeReportAsync_GroupsByZoneAndServiceLevel()
    {
        var timestamp = UtcTimestamp(ReportDate);

        var source = new FakeReportDataSource
        {
            Shipments =
            [
                new("IND01", "NORTH", "Express", timestamp),
                new("IND01", "NORTH", "Express", timestamp),
                new("IND01", "NORTH", "Standard", timestamp),
                new("BPL01", "SOUTH", "Express", timestamp)
            ]
        };

        var report = await new ReportingService(source)
            .GetShippingVolumeReportAsync(ReportDate, "IND01");

        Assert.Equal(2, report.Count);
        Assert.Contains(report, item =>
            item.DestinationZone == "NORTH"
            && item.ServiceLevel == "Express"
            && item.ShipmentCount == 2);
        Assert.Contains(report, item =>
            item.DestinationZone == "NORTH"
            && item.ServiceLevel == "Standard"
            && item.ShipmentCount == 1);
    }

    [Fact]
    public async Task GetShippingVolumeReportAsync_ExcludesOtherDates()
    {
        var source = new FakeReportDataSource
        {
            Shipments =
            [
                new("IND01", "NORTH", "Express",
                    UtcTimestamp(ReportDate.AddDays(-1)))
            ]
        };

        var report = await new ReportingService(source)
            .GetShippingVolumeReportAsync(ReportDate);

        Assert.Empty(report);
    }

    [Fact]
    public async Task GetBranchServiceActivityReportAsync_CountsBothActivityTypes()
    {
        var timestamp = UtcTimestamp(ReportDate);

        var source = new FakeReportDataSource
        {
            MoneyOrders =
            [
                new("IND01", "MO001", timestamp),
                new("IND01", "MO002", timestamp),
                new("BPL01", "MO003", timestamp)
            ],
            Verifications =
            [
                new("IND01", "IDV001", timestamp),
                new("IND01", "IDV002", timestamp),
                new("IND01", "IDV003", timestamp),
                new("BPL01", "IDV004", timestamp)
            ]
        };

        var report = await new ReportingService(source)
            .GetBranchServiceActivityReportAsync(ReportDate);

        Assert.Equal(2, report.Count);

        var indore = Assert.Single(
            report,
            item => item.BranchCode == "IND01"); Assert.Equal(2, indore.MoneyOrderCount);
        Assert.Equal(3, indore.IdentityVerificationCount);

        var bhopal = Assert.Single(
            report,
            item => item.BranchCode == "BPL01"); Assert.Equal(1, bhopal.MoneyOrderCount);
        Assert.Equal(1, bhopal.IdentityVerificationCount);
    }

    [Fact]
    public async Task GetBranchServiceActivityReportAsync_ExcludesOtherDates()
    {
        var source = new FakeReportDataSource
        {
            MoneyOrders =
            [
                new("IND01", "MO001", UtcTimestamp(ReportDate.AddDays(-1)))
            ]
        };

        var report = await new ReportingService(source)
            .GetBranchServiceActivityReportAsync(ReportDate);

        Assert.Empty(report);
    }

    [Fact]
    public async Task GetLowStockReportAsync_WhenCancelled_Throws()
    {
        var service = new ReportingService(new FakeReportDataSource());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetLowStockReportAsync(cancellationToken: cancellation.Token));
    }

    private static ReportTransaction Transaction(
        string branch,
        DateOnly date,
        ReportSaleCategory category,
        decimal total,
        bool completed = true) =>
        new(branch, UtcTimestamp(date), category, total, completed);

    private static DateTimeOffset UtcTimestamp(DateOnly date) =>
        new(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    private sealed class FakeReportDataSource : IReportDataSource
    {
        public IReadOnlyList<ReportTransaction> Transactions { get; init; } = [];
        public IReadOnlyList<ReportInventoryItem> Inventory { get; init; } = [];
        public IReadOnlyList<ReportShipment> Shipments { get; init; } = [];
        public IReadOnlyList<ReportMoneyOrder> MoneyOrders { get; init; } = [];
        public IReadOnlyList<ReportIdentityVerification> Verifications { get; init; } = [];

        public Task<IReadOnlyList<ReportTransaction>> GetTransactionsAsync(
            CancellationToken cancellationToken = default) =>
            ReturnAsync(Transactions, cancellationToken);

        public Task<IReadOnlyList<ReportInventoryItem>> GetInventoryAsync(
            CancellationToken cancellationToken = default) =>
            ReturnAsync(Inventory, cancellationToken);

        public Task<IReadOnlyList<ReportShipment>> GetShipmentsAsync(
            CancellationToken cancellationToken = default) =>
            ReturnAsync(Shipments, cancellationToken);

        public Task<IReadOnlyList<ReportMoneyOrder>> GetMoneyOrdersAsync(
            CancellationToken cancellationToken = default) =>
            ReturnAsync(MoneyOrders, cancellationToken);

        public Task<IReadOnlyList<ReportIdentityVerification>>
            GetIdentityVerificationsAsync(
                CancellationToken cancellationToken = default) =>
            ReturnAsync(Verifications, cancellationToken);

        private static Task<IReadOnlyList<T>> ReturnAsync<T>(
            IReadOnlyList<T> result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }
}
