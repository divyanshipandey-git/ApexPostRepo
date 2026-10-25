using ApexPost.Application.Abstractions.Reports;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Reports;
using ApexPost.Domain.Entities;

namespace ApexPost.Infrastructure.Persistence;

public sealed class JsonReportDataSource : IReportDataSource
{
    private const int LowStockReorderLevel = 5;
    private readonly IBranchRepository _branches;
    private readonly ICounterTransactionRepository _transactions;
    private readonly IInventoryRepository _inventory;
    private readonly IProductRepository _products;
    private readonly IShipmentRepository _shipments;
    private readonly IMoneyOrderRepository _moneyOrders;

    public JsonReportDataSource(
        IBranchRepository branches,
        ICounterTransactionRepository transactions,
        IInventoryRepository inventory,
        IProductRepository products,
        IShipmentRepository shipments,
        IMoneyOrderRepository moneyOrders)
    {
        ArgumentNullException.ThrowIfNull(branches);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(shipments);
        ArgumentNullException.ThrowIfNull(moneyOrders);
        _branches = branches;
        _transactions = transactions;
        _inventory = inventory;
        _products = products;
        _shipments = shipments;
        _moneyOrders = moneyOrders;
    }

    public async Task<IReadOnlyList<ReportTransaction>> GetTransactionsAsync(
        CancellationToken cancellationToken = default)
    {
        var transactions = await ForBranchesAsync(
            (branch, token) => _transactions.GetByBranchAsync(branch, token),
            cancellationToken);

        return transactions
            .Where(transaction => transaction.Status == TransactionStatus.Paid &&
                transaction.OccurredAtUtc.HasValue)
            .SelectMany(transaction => transaction.LineItems
                .Where(line => line.ItemType is "Retail" or "Service")
                .GroupBy(line => line.ItemType == "Retail"
                    ? ReportSaleCategory.Retail
                    : ReportSaleCategory.Service)
                .Select(group => new ReportTransaction(
                    transaction.Id,
                    transaction.BranchCode.Value,
                    transaction.OccurredAtUtc!.Value,
                    group.Key,
                    group.Sum(line => line.LineTotal.Amount + CalculateTax(transaction, line)),
                    IsCompleted: true)))
            .ToArray();
    }

    public async Task<IReadOnlyList<ReportInventoryItem>> GetInventoryAsync(
        CancellationToken cancellationToken = default)
    {
        var inventoryTask = ForBranchesAsync(
            (branch, token) => _inventory.GetByBranchAsync(branch, token),
            cancellationToken);
        var productsTask = _products.GetAllAsync(cancellationToken);
        await Task.WhenAll(inventoryTask, productsTask);
        var productNames = (await productsTask).ToDictionary(
            product => product.Sku.Value,
            product => product.Name,
            StringComparer.OrdinalIgnoreCase);

        return (await inventoryTask)
            .Select(stock => new ReportInventoryItem(
                stock.BranchCode.Value,
                stock.Sku.Value,
                productNames.GetValueOrDefault(stock.Sku.Value, stock.Sku.Value),
                stock.Quantity,
                LowStockReorderLevel))
            .ToArray();
    }

    public async Task<IReadOnlyList<ReportShipment>> GetShipmentsAsync(
        CancellationToken cancellationToken = default)
    {
        var shipments = await ForBranchesAsync(
            (branch, token) => _shipments.GetByBranchAsync(branch, token),
            cancellationToken);

        return shipments
            .Where(shipment => shipment.BranchCode is not null && shipment.CreatedAtUtc.HasValue)
            .Select(shipment => new ReportShipment(
                shipment.BranchCode!.Value,
                shipment.DestinationZone,
                shipment.ServiceLevel.ToString(),
                shipment.CreatedAtUtc!.Value))
            .ToArray();
    }

    public async Task<IReadOnlyList<ReportMoneyOrder>> GetMoneyOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        var orders = await ForBranchesAsync(
            (branch, token) => _moneyOrders.GetByBranchAsync(branch, token),
            cancellationToken);

        return orders
            .Where(order => order.CreatedAtUtc.HasValue && !string.IsNullOrWhiteSpace(order.BranchCode))
            .Select(order => new ReportMoneyOrder(
                order.BranchCode!,
                order.Id,
                order.CreatedAtUtc!.Value))
            .ToArray();
    }

    public async Task<IReadOnlyList<ReportIdentityVerification>> GetIdentityVerificationsAsync(
        CancellationToken cancellationToken = default)
    {
        var orders = await ForBranchesAsync(
            (branch, token) => _moneyOrders.GetByBranchAsync(branch, token),
            cancellationToken);

        return orders
            .Where(order => order.IdentityVerification is not null &&
                !string.IsNullOrWhiteSpace(order.BranchCode))
            .Select(order => new ReportIdentityVerification(
                order.BranchCode!,
                order.IdentityVerification!.Id,
                order.IdentityVerification.VerifiedAt))
            .ToArray();
    }

    private async Task<IReadOnlyList<T>> ForBranchesAsync<T>(
        Func<string, CancellationToken, Task<IReadOnlyList<T>>> getByBranch,
        CancellationToken cancellationToken)
    {
        var branches = await _branches.GetAllAsync(cancellationToken);
        var results = await Task.WhenAll(branches.Select(async branch =>
            await getByBranch(branch.Code.Value, cancellationToken)));
        return results.SelectMany(result => result).ToArray();
    }

    private static decimal CalculateTax(CounterTransaction transaction, TransactionLineItem line)
    {
        var taxRate = transaction.TaxRates.FirstOrDefault(rate => rate.Category == line.TaxCategory);
        return taxRate?.CalculateTax(line.LineTotal.Amount) ?? 0m;
    }
}
