using ApexPost.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

public static class SqlQueryDiagnostics
{
    public static IReadOnlyDictionary<string, string> Capture(ApexPostDbContext db)
    {
        var sales = db.CounterTransactions.AsNoTracking()
            .Where(transaction => transaction.Status == TransactionStatus.Paid &&
                transaction.OccurredAtUtc >= DateTimeOffset.UtcNow.AddDays(-30))
            .GroupBy(transaction => transaction.BranchCode)
            .Select(group => new { Branch = group.Key, Count = group.LongCount(), Gross = group.Sum(item => item.Total) });
        var lowStock = from stock in db.BranchStock.AsNoTracking()
                       join product in db.Products.AsNoTracking() on stock.Sku equals product.Sku
                       where stock.Quantity <= 5
                       select new { stock.BranchCode, stock.Sku, product.Name, stock.Quantity };
        var shipments = db.Shipments.AsNoTracking()
            .Where(shipment => shipment.CreatedAtUtc >= DateTimeOffset.UtcNow.AddDays(-30))
            .GroupBy(shipment => new { shipment.DestinationZone, shipment.ServiceLevel })
            .Select(group => new { group.Key.DestinationZone, group.Key.ServiceLevel, Count = group.LongCount() });
        var auditorMoneyOrders = db.MoneyOrders.AsNoTracking()
            .Where(order => order.CreatedAtUtc >= DateTimeOffset.UtcNow.AddDays(-30))
            .OrderByDescending(order => order.CreatedAtUtc)
            .Select(order => new
            {
                order.Id,
                order.BranchCode,
                order.Amount,
                order.Fee,
                order.TransactionId,
                order.CreatedAtUtc
            });
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Paid branch sales"] = sales.ToQueryString(),
            ["Low stock with product name"] = lowStock.ToQueryString(),
            ["Shipment volume by zone and service"] = shipments.ToQueryString(),
            ["Auditor money order search"] = auditorMoneyOrders.ToQueryString()
        };
    }
}
