
using ApexPost.Application.Reports;

namespace ApexPost.Application.Abstractions.Reports;

public interface IReportDataSource
{
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
