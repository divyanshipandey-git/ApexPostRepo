using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Infrastructure.Persistence.SqlEntities;

public sealed class RoleRecord
{
    public StaffRole RoleCode { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class ProductCategoryRecord
{
    public ProductCategory CategoryCode { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class ShippingZoneRecord
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ShippingRateRecord> Rates { get; set; } = [];
}

public sealed class ShippingRateRecord
{
    public string ZoneCode { get; set; } = string.Empty;
    public ShippingServiceLevel ServiceLevel { get; set; }
    public decimal BasePrice { get; set; }
    public decimal PricePerKilogram { get; set; }
}

public sealed class StockMovementRecord
{
    public long Id { get; set; }
    public BranchCode BranchCode { get; set; } = new("X");
    public Sku Sku { get; set; } = new("SKU");
    public int QuantityDelta { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? Reason { get; set; }
    public string? ChangedBy { get; set; }
}

public sealed class AuditEntryRecord
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? StaffId { get; set; }
    public string? Role { get; set; }
    public string? BranchCode { get; set; }
    public string Controller { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string RouteValuesJson { get; set; } = "{}";
    public int StatusCode { get; set; }
    public long DurationMilliseconds { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class ImportRunRecord
{
    public long Id { get; set; }
    public string SourceFingerprint { get; set; } = string.Empty;
    public DateTimeOffset ImportedAtUtc { get; set; }
    public string ImporterVersion { get; set; } = string.Empty;
    public int SourceCount { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
}
