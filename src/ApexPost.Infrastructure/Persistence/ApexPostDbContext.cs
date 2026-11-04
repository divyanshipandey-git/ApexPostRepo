using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence.SqlEntities;
using ApexPost.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

public sealed class ApexPostDbContext(DbContextOptions<ApexPostDbContext> options) : DbContext(options)
{
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<StaffCredential> StaffCredentials => Set<StaffCredential>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryStock> BranchStock => Set<InventoryStock>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<CounterTransactionRecord> CounterTransactions => Set<CounterTransactionRecord>();
    public DbSet<TransactionLineRecord> TransactionLines => Set<TransactionLineRecord>();
    public DbSet<TransactionTaxRateRecord> TransactionTaxRates => Set<TransactionTaxRateRecord>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<MoneyOrderRecord> MoneyOrders => Set<MoneyOrderRecord>();
    public DbSet<IdentityVerificationRecord> IdentityVerifications => Set<IdentityVerificationRecord>();
    public DbSet<RoleRecord> Roles => Set<RoleRecord>();
    public DbSet<ProductCategoryRecord> ProductCategories => Set<ProductCategoryRecord>();
    public DbSet<ShippingZoneRecord> ShippingZones => Set<ShippingZoneRecord>();
    public DbSet<ShippingRateRecord> ShippingRates => Set<ShippingRateRecord>();
    public DbSet<StockMovementRecord> StockMovements => Set<StockMovementRecord>();
    public DbSet<AuditEntryRecord> AuditEntries => Set<AuditEntryRecord>();
    public DbSet<ImportRunRecord> ImportRuns => Set<ImportRunRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApexPostDbContext).Assembly);

        // Keyless entities for stored procedure results
        modelBuilder.Entity<DailyBranchSalesProcedureRow>().HasNoKey();
        modelBuilder.Entity<LowStockProcedureRow>().HasNoKey();
        modelBuilder.Entity<TopProductRevenueProcedureRow>().HasNoKey();
    }
}
