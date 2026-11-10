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
    public DbSet<TrackedShipment> TrackedShipments => Set<TrackedShipment>();
    public DbSet<TrackedShipmentScan> TrackedShipmentScans => Set<TrackedShipmentScan>();
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

        // TrackedShipment configuration
        modelBuilder.Entity<TrackedShipment>(entity =>
        {
            entity.ToTable("TrackedShipments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(128).IsUnicode(false);
            entity.Property(e => e.TrackingNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.HasIndex(e => e.TrackingNumber).IsUnique().HasDatabaseName("IX_TrackedShipments_TrackingNumber");
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.WeightKg).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.RecipientName).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(e => e.RecipientPhone).HasMaxLength(50).IsUnicode(false).IsRequired();
            entity.Property(e => e.RecipientAddress).HasMaxLength(200).IsUnicode(false).IsRequired();
            entity.Property(e => e.RecipientCity).HasMaxLength(50).IsUnicode(false).IsRequired();
            entity.Property(e => e.RecipientPostalCode).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.RecipientCountry).HasMaxLength(2).IsUnicode(false).IsRequired().HasDefaultValue("US");
            entity.Property(e => e.SpecialInstructions).HasMaxLength(500).IsUnicode(false);
            entity.Property(e => e.DeclaredValue).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("IX_TrackedShipments_CreatedAt");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_TrackedShipments_Status");

            entity.HasMany(e => e.Scans)
                .WithOne()
                .HasForeignKey(s => s.TrackedShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrackedShipmentScan>(entity =>
        {
            entity.ToTable("TrackedShipmentScans");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(128).IsUnicode(false);
            entity.Property(e => e.TrackedShipmentId).HasMaxLength(128).IsUnicode(false).IsRequired();
            entity.Property(e => e.ScanType).IsRequired().HasConversion<int>();
            entity.Property(e => e.CourierId).HasMaxLength(50).IsUnicode(false).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(200).IsUnicode(false).IsRequired();
            entity.Property(e => e.ScannedAtUtc).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500).IsUnicode(false);
            entity.Property(e => e.Latitude).HasPrecision(9, 6);
            entity.Property(e => e.Longitude).HasPrecision(9, 6);
            entity.Property(e => e.RecipientSignature).HasMaxLength(100).IsUnicode(false);
            entity.HasIndex(e => e.TrackedShipmentId).HasDatabaseName("IX_TrackedShipmentScans_ShipmentId");
            entity.HasIndex(e => e.ScannedAtUtc).HasDatabaseName("IX_TrackedShipmentScans_ScannedAt");
            entity.HasIndex(e => e.ScanType).HasDatabaseName("IX_TrackedShipmentScans_ScanType");
            entity.HasIndex(e => e.CourierId).HasDatabaseName("IX_TrackedShipmentScans_CourierId");
        });
    }
}
