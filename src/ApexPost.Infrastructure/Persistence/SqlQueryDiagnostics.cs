using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

public static class SqlQueryDiagnostics
{
    public static IReadOnlyDictionary<string, string> Capture(ApexPostDbContext db)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var branch = new BranchCode("P01");
        var shipments = db.Shipments.AsNoTracking()
            .Where(shipment => shipment.CreatedAtUtc >= start && shipment.CreatedAtUtc < end)
            .Where(shipment => shipment.BranchCode == branch)
            .GroupBy(shipment => new { shipment.DestinationZone, shipment.ServiceLevel })
            .Select(group => new
            {
                group.Key.DestinationZone,
                group.Key.ServiceLevel,
                ShipmentCount = group.Count()
            });
        var moneyOrderActivity = db.MoneyOrders.AsNoTracking()
            .Where(order =>
                order.CreatedAtUtc >= start &&
                order.CreatedAtUtc < end &&
                order.BranchCode != null)
            .Where(order => order.BranchCode == branch)
            .GroupBy(order => order.BranchCode)
            .Select(group => new { BranchCode = group.Key, Count = group.Count() });
        var identityVerificationActivity =
            from order in db.MoneyOrders.AsNoTracking()
            join identity in db.IdentityVerifications.AsNoTracking()
                on order.Id equals identity.MoneyOrderId
            where order.BranchCode != null &&
                  identity.VerifiedAtUtc >= start &&
                  identity.VerifiedAtUtc < end
            select new { BranchCode = order.BranchCode };
        identityVerificationActivity = identityVerificationActivity
            .Where(item => item.BranchCode == branch);
        var identityVerificationCounts = identityVerificationActivity
            .GroupBy(item => item.BranchCode)
            .Select(group => new { BranchCode = group.Key, Count = group.Count() });

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Filtered shipment volume"] = shipments.ToQueryString(),
            ["Money-order activity by branch"] = moneyOrderActivity.ToQueryString(),
            ["Identity verification activity by branch"] = identityVerificationCounts.ToQueryString()
        };
    }
}
