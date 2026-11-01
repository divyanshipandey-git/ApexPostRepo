using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence.SqlEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApexPost.Infrastructure.Persistence;

internal static class SqlMapping
{
    public static readonly ValueConverter<BranchCode, string> BranchCodeConverter =
        new(value => value.Value, value => new BranchCode(value));
    public static readonly ValueConverter<BranchCode?, string> NullableBranchCodeConverter =
        new(value => value!.Value, value => new BranchCode(value));
    public static readonly ValueConverter<Sku, string> SkuConverter =
        new(value => value.Value, value => new Sku(value));
    public static readonly ValueConverter<Money, decimal> MoneyConverter =
        new(value => value.Amount, value => new Money(value));
    public static readonly ValueConverter<Weight, decimal> WeightConverter =
        new(value => value.Kilograms, value => new Weight(value));
    public static readonly ValueConverter<DateTimeOffset, DateTime> UtcDateTimeConverter =
        new(value => value.UtcDateTime, value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

    public static void AddAuditColumns<TEntity>(EntityTypeBuilder<TEntity> builder, bool modified = false)
        where TEntity : class
    {
        if (builder.Metadata.FindProperty("CreatedAtUtc") is null)
        {
            builder.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("datetime2(7)")
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .IsRequired();
        }
        builder.Property<string?>("CreatedBy").HasMaxLength(128).IsUnicode(false);
        if (modified)
        {
            builder.Property<DateTime?>("ModifiedAtUtc").HasColumnType("datetime2(7)");
        }
    }
}

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches", table => table.HasCheckConstraint("CK_Branches_OpeningHours", "[ClosingTime] > [OpeningTime]"));
        builder.HasKey(branch => branch.Code).HasName("PK_Branches");
        builder.Property(branch => branch.Code).HasConversion(SqlMapping.BranchCodeConverter).HasColumnName("BranchCode").HasMaxLength(10).IsUnicode(false);
        builder.Property(branch => branch.Name).HasMaxLength(200).IsRequired();
        builder.Property(branch => branch.Region).HasMaxLength(100).IsRequired();
        builder.Property(branch => branch.OpeningTime).HasColumnType("time(0)");
        builder.Property(branch => branch.ClosingTime).HasColumnType("time(0)");
        builder.Property(branch => branch.IsActive).HasDefaultValue(true);
        SqlMapping.AddAuditColumns(builder, modified: true);
    }
}

public sealed class CounterConfiguration : IEntityTypeConfiguration<Counter>
{
    public void Configure(EntityTypeBuilder<Counter> builder)
    {
        builder.ToTable("Counters");
        builder.HasKey(counter => counter.Id).HasName("PK_Counters");
        builder.Property(counter => counter.Id).HasMaxLength(128).IsUnicode(false);
        builder.Property(counter => counter.BranchCode).HasConversion(SqlMapping.BranchCodeConverter).HasMaxLength(10).IsUnicode(false);
        builder.Property(counter => counter.Name).HasMaxLength(200).IsRequired();
        builder.Property(counter => counter.IsActive).HasDefaultValue(true);
        builder.HasOne<Branch>().WithMany().HasForeignKey(counter => counter.BranchCode).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Counters_Branches");
        SqlMapping.AddAuditColumns(builder, modified: true);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<RoleRecord>
{
    public void Configure(EntityTypeBuilder<RoleRecord> builder)
    {
        builder.ToTable("Roles", table => table.HasCheckConstraint("CK_Roles_Code", "[RoleCode] IN ('CounterClerk','BranchManager','Auditor','Administrator')"));
        builder.HasKey(role => role.RoleCode).HasName("PK_Roles");
        builder.Property(role => role.RoleCode).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(role => role.Name).HasMaxLength(100).IsRequired();
        builder.HasData(
            new RoleRecord { RoleCode = StaffRole.CounterClerk, Name = "Counter Clerk" },
            new RoleRecord { RoleCode = StaffRole.BranchManager, Name = "Branch Manager" },
            new RoleRecord { RoleCode = StaffRole.Auditor, Name = "Auditor" },
            new RoleRecord { RoleCode = StaffRole.Administrator, Name = "Administrator" });
        SqlMapping.AddAuditColumns(builder);
    }
}

public sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("Staff");
        builder.HasKey(staff => staff.Id).HasName("PK_Staff");
        builder.Property(staff => staff.Id).HasMaxLength(128).IsUnicode(false);
        builder.Property(staff => staff.Name).HasMaxLength(200).IsRequired();
        builder.Property(staff => staff.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(staff => staff.Email).IsUnique().HasDatabaseName("UX_Staff_Email");
        builder.Property(staff => staff.Role).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(staff => staff.HomeBranchCode).HasConversion(SqlMapping.BranchCodeConverter).HasMaxLength(10).IsUnicode(false);
        builder.Property(staff => staff.IsActive).HasDefaultValue(true);
        builder.HasOne<RoleRecord>().WithMany().HasForeignKey(staff => staff.Role).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Staff_Roles");
        builder.HasOne<Branch>().WithMany().HasForeignKey(staff => staff.HomeBranchCode).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Staff_Branches");
        SqlMapping.AddAuditColumns(builder, modified: true);
    }
}

public sealed class StaffCredentialConfiguration : IEntityTypeConfiguration<StaffCredential>
{
    public void Configure(EntityTypeBuilder<StaffCredential> builder)
    {
        builder.ToTable("StaffCredentials", table => table.HasCheckConstraint("CK_StaffCredentials_Hash", "LEN([PasswordHash]) > 0"));
        builder.HasKey(credential => credential.StaffId).HasName("PK_StaffCredentials");
        builder.Property(credential => credential.StaffId).HasMaxLength(128).IsUnicode(false);
        builder.Property(credential => credential.PasswordHash).HasMaxLength(512).IsRequired();
        builder.HasOne<Staff>().WithOne().HasForeignKey<StaffCredential>(credential => credential.StaffId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_StaffCredentials_Staff");
        SqlMapping.AddAuditColumns(builder, modified: true);
    }
}

public sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategoryRecord>
{
    public void Configure(EntityTypeBuilder<ProductCategoryRecord> builder)
    {
        builder.ToTable("ProductCategories", table => table.HasCheckConstraint("CK_ProductCategories_Code", "[CategoryCode] IN ('Packaging','Stationery','OfficeSupplies')"));
        builder.HasKey(category => category.CategoryCode).HasName("PK_ProductCategories");
        builder.Property(category => category.CategoryCode).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(category => category.Name).HasMaxLength(100).IsRequired();
        builder.HasData(
            new ProductCategoryRecord { CategoryCode = ProductCategory.Packaging, Name = "Packaging" },
            new ProductCategoryRecord { CategoryCode = ProductCategory.Stationery, Name = "Stationery" },
            new ProductCategoryRecord { CategoryCode = ProductCategory.OfficeSupplies, Name = "Office Supplies" });
        SqlMapping.AddAuditColumns(builder);
    }
}

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_UnitPrice", "[UnitPrice] >= 0");
            table.HasCheckConstraint("CK_Products_TaxCategory", "[TaxCategory] IN ('Standard','Reduced','ZeroRated','Exempt')");
        });
        builder.HasKey(product => product.Sku).HasName("PK_Products");
        builder.Property(product => product.Sku).HasConversion(SqlMapping.SkuConverter).HasColumnName("Sku").HasMaxLength(20).IsUnicode(false);
        builder.Property(product => product.Name).HasMaxLength(200).IsRequired();
        builder.Property(product => product.Category).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(product => product.UnitPrice).HasConversion(SqlMapping.MoneyConverter).HasPrecision(18, 2);
        builder.Property(product => product.TaxCategory).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.HasOne<ProductCategoryRecord>().WithMany().HasForeignKey(product => product.Category).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Products_ProductCategories");
        SqlMapping.AddAuditColumns(builder, modified: true);
    }
}

public sealed class BranchStockConfiguration : IEntityTypeConfiguration<InventoryStock>
{
    public void Configure(EntityTypeBuilder<InventoryStock> builder)
    {
        builder.ToTable("BranchStock", table => table.HasCheckConstraint("CK_BranchStock_Quantity_NonNegative", "[Quantity] >= 0"));
        builder.HasKey(stock => new { stock.BranchCode, stock.Sku }).HasName("PK_BranchStock");
        builder.Property(stock => stock.BranchCode).HasConversion(SqlMapping.BranchCodeConverter).HasMaxLength(10).IsUnicode(false);
        builder.Property(stock => stock.Sku).HasConversion(SqlMapping.SkuConverter).HasMaxLength(20).IsUnicode(false);
        builder.Property(stock => stock.Quantity).IsRequired();
        builder.Property<byte[]>("RowVersion").IsRowVersion().IsConcurrencyToken();
        builder.HasOne<Branch>().WithMany().HasForeignKey(stock => stock.BranchCode).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_BranchStock_Branches");
        builder.HasOne<Product>().WithMany().HasForeignKey(stock => stock.Sku).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_BranchStock_Products");
        builder.HasIndex(stock => new { stock.BranchCode, stock.Quantity, stock.Sku }).HasDatabaseName("IX_BranchStock_Branch_Quantity_Sku");
        SqlMapping.AddAuditColumns(builder, modified: true);
    }
}

public sealed class ShippingZoneConfiguration : IEntityTypeConfiguration<ShippingZoneRecord>
{
    public void Configure(EntityTypeBuilder<ShippingZoneRecord> builder)
    {
        builder.ToTable("ShippingZones");
        builder.HasKey(zone => zone.Code).HasName("PK_ShippingZones");
        builder.Property(zone => zone.Code).HasMaxLength(32).IsUnicode(false);
        builder.Property(zone => zone.Name).HasMaxLength(100).IsRequired();
        SqlMapping.AddAuditColumns(builder, modified: true);
    }
}

public sealed class ShippingRateConfiguration : IEntityTypeConfiguration<ShippingRateRecord>
{
    public void Configure(EntityTypeBuilder<ShippingRateRecord> builder)
    {
        builder.ToTable("ShippingRates", table =>
        {
            table.HasCheckConstraint("CK_ShippingRates_Prices", "[BasePrice] >= 0 AND [PricePerKilogram] >= 0");
            table.HasCheckConstraint("CK_ShippingRates_ServiceLevel", "[ServiceLevel] IN ('Standard','Express','Registered')");
        });
        builder.HasKey(rate => new { rate.ZoneCode, rate.ServiceLevel }).HasName("PK_ShippingRates");
        builder.Property(rate => rate.ZoneCode).HasMaxLength(32).IsUnicode(false);
        builder.Property(rate => rate.ServiceLevel).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(rate => rate.BasePrice).HasPrecision(18, 2);
        builder.Property(rate => rate.PricePerKilogram).HasPrecision(18, 2);
        builder.HasOne<ShippingZoneRecord>().WithMany(zone => zone.Rates).HasForeignKey(rate => rate.ZoneCode).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ShippingRates_ShippingZones");
        SqlMapping.AddAuditColumns(builder, modified: true);
    }
}

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments", table =>
        {
            table.HasCheckConstraint("CK_Shipments_Weight", "[WeightKg] >= 0");
            table.HasCheckConstraint("CK_Shipments_Dimensions", "[LengthCm] > 0 AND [WidthCm] > 0 AND [HeightCm] > 0");
            table.HasCheckConstraint("CK_Shipments_ItemType", "[ItemType] IN ('Parcel','Letter')");
            table.HasCheckConstraint("CK_Shipments_ServiceLevel", "[ServiceLevel] IN ('Standard','Express','Registered')");
        });
        builder.HasKey(shipment => shipment.Id).HasName("PK_Shipments");
        builder.Property(shipment => shipment.Id).HasMaxLength(128).IsUnicode(false);
        builder.Property(shipment => shipment.ItemType).HasMaxLength(16).IsUnicode(false);
        builder.Property(shipment => shipment.Weight).HasConversion(SqlMapping.WeightConverter).HasColumnName("WeightKg").HasPrecision(9, 3);
        builder.Property(shipment => shipment.LengthCm).HasPrecision(9, 2);
        builder.Property(shipment => shipment.WidthCm).HasPrecision(9, 2);
        builder.Property(shipment => shipment.HeightCm).HasPrecision(9, 2);
        builder.Property(shipment => shipment.DestinationZone).HasColumnName("ZoneCode").HasMaxLength(32).IsUnicode(false);
        builder.Property(shipment => shipment.ServiceLevel).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(shipment => shipment.BranchCode).HasConversion(SqlMapping.NullableBranchCodeConverter).HasMaxLength(10).IsUnicode(false);
        builder.Property(shipment => shipment.CounterTransactionId).HasMaxLength(128).IsUnicode(false);
        builder.Property(shipment => shipment.CourierStatus).HasMaxLength(100);
        builder.Property(shipment => shipment.CourierStatusUpdatedAtUtc).HasColumnType("datetime2(7)").HasConversion(SqlMapping.UtcDateTimeConverter);
        builder.Property(shipment => shipment.CreatedAtUtc).HasColumnType("datetime2(7)").HasConversion(SqlMapping.UtcDateTimeConverter);
        builder.HasOne<Branch>().WithMany().HasForeignKey(shipment => shipment.BranchCode).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Shipments_Branches");
        builder.HasOne<ShippingZoneRecord>().WithMany().HasForeignKey(shipment => shipment.DestinationZone).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Shipments_ShippingZones");
        builder.HasOne<CounterTransactionRecord>().WithMany().HasForeignKey(shipment => shipment.CounterTransactionId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Shipments_CounterTransactions");
        builder.HasIndex(shipment => new { shipment.DestinationZone, shipment.ServiceLevel, shipment.CreatedAtUtc }).HasDatabaseName("IX_Shipments_Zone_Service_Created");
        SqlMapping.AddAuditColumns(builder, modified: true);
    }
}

public sealed class CounterTransactionRecordConfiguration : IEntityTypeConfiguration<CounterTransactionRecord>
{
    public void Configure(EntityTypeBuilder<CounterTransactionRecord> builder)
    {
        builder.ToTable("CounterTransactions", table =>
        {
            table.HasCheckConstraint("CK_CounterTransactions_Status", "[Status] IN ('Open','Paid','Voided','Refunded')");
            table.HasCheckConstraint("CK_CounterTransactions_Totals", "[Subtotal] >= 0 AND [Tax] >= 0 AND [Total] >= 0");
        });
        builder.HasKey(transaction => transaction.Id).HasName("PK_CounterTransactions");
        builder.Property(transaction => transaction.Id).HasMaxLength(128).IsUnicode(false);
        builder.Property(transaction => transaction.BranchCode).HasConversion(SqlMapping.BranchCodeConverter).HasMaxLength(10).IsUnicode(false);
        builder.Property(transaction => transaction.CounterId).HasMaxLength(128).IsUnicode(false);
        builder.Property(transaction => transaction.ClerkId).HasMaxLength(128).IsUnicode(false);
        builder.Property(transaction => transaction.Status).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(transaction => transaction.Reason).HasMaxLength(500);
        builder.Property(transaction => transaction.OccurredAtUtc).HasColumnType("datetime2(7)").HasConversion(SqlMapping.UtcDateTimeConverter);
        builder.Property(transaction => transaction.Subtotal).HasPrecision(18, 2);
        builder.Property(transaction => transaction.Tax).HasPrecision(18, 2);
        builder.Property(transaction => transaction.Total).HasPrecision(18, 2);
        builder.Property(transaction => transaction.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne<Branch>().WithMany().HasForeignKey(transaction => transaction.BranchCode).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CounterTransactions_Branches");
        builder.HasOne<Counter>().WithMany().HasForeignKey(transaction => transaction.CounterId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CounterTransactions_Counters");
        builder.HasOne<Staff>().WithMany().HasForeignKey(transaction => transaction.ClerkId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CounterTransactions_Staff");
        builder.HasMany(transaction => transaction.Lines).WithOne().HasForeignKey(line => line.TransactionId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TransactionLines_CounterTransactions");
        builder.HasMany(transaction => transaction.TaxRates).WithOne().HasForeignKey(rate => rate.TransactionId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TransactionTaxRates_CounterTransactions");
        builder.HasOne(transaction => transaction.Payment).WithOne().HasForeignKey<PaymentRecord>(payment => payment.TransactionId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Payments_CounterTransactions");
        builder.HasIndex(transaction => new { transaction.BranchCode, transaction.OccurredAtUtc, transaction.Status }).HasDatabaseName("IX_CounterTransactions_Branch_Occurred_Status");
        SqlMapping.AddAuditColumns(builder, modified: true);
    }
}

public sealed class TransactionLineConfiguration : IEntityTypeConfiguration<TransactionLineRecord>
{
    public void Configure(EntityTypeBuilder<TransactionLineRecord> builder)
    {
        builder.ToTable("TransactionLines", table =>
        {
            table.HasCheckConstraint("CK_TransactionLines_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_TransactionLines_Amounts", "[UnitPrice] >= 0 AND [LineTotal] >= 0");
            table.HasCheckConstraint("CK_TransactionLines_ItemType", "[ItemType] IN ('Retail','Service')");
            table.HasCheckConstraint("CK_TransactionLines_TaxCategory", "[TaxCategory] IN ('Standard','Reduced','ZeroRated','Exempt')");
        });
        builder.HasKey(line => line.Id).HasName("PK_TransactionLines");
        builder.Property(line => line.Id).ValueGeneratedOnAdd();
        builder.Property(line => line.TransactionId).HasMaxLength(128).IsUnicode(false);
        builder.Property(line => line.Description).HasMaxLength(500).IsRequired();
        builder.Property(line => line.ItemType).HasMaxLength(16).IsUnicode(false);
        builder.Property(line => line.UnitPrice).HasPrecision(18, 2);
        builder.Property(line => line.TaxCategory).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(line => line.ReferenceId).HasMaxLength(128).IsUnicode(false);
        builder.Property(line => line.LineTotal).HasPrecision(18, 2);
        builder.HasIndex(line => new { line.TransactionId, line.LineNumber }).IsUnique().HasDatabaseName("UX_TransactionLines_Transaction_LineNumber");
        builder.HasIndex(line => new { line.TransactionId, line.ItemType }).IncludeProperties(line => new { line.LineTotal, line.Quantity }).HasDatabaseName("IX_TransactionLines_Transaction_ItemType");
        SqlMapping.AddAuditColumns(builder);
    }
}

public sealed class TransactionTaxRateConfiguration : IEntityTypeConfiguration<TransactionTaxRateRecord>
{
    public void Configure(EntityTypeBuilder<TransactionTaxRateRecord> builder)
    {
        builder.ToTable("TransactionTaxRates", table => table.HasCheckConstraint("CK_TransactionTaxRates_Rate", "[Rate] >= 0 AND [Rate] <= 1"));
        builder.HasKey(rate => new { rate.TransactionId, rate.Category }).HasName("PK_TransactionTaxRates");
        builder.Property(rate => rate.TransactionId).HasMaxLength(128).IsUnicode(false);
        builder.Property(rate => rate.Category).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(rate => rate.Rate).HasPrecision(9, 6);
        SqlMapping.AddAuditColumns(builder);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Method", "[Method] IN ('Cash','Card','Upi')");
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
        });
        builder.HasKey(payment => payment.TransactionId).HasName("PK_Payments");
        builder.Property(payment => payment.TransactionId).HasMaxLength(128).IsUnicode(false);
        builder.Property(payment => payment.Method).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.PaidAtUtc).HasColumnType("datetime2(7)").HasConversion(SqlMapping.UtcDateTimeConverter);
        SqlMapping.AddAuditColumns(builder);
    }
}

public sealed class MoneyOrderRecordConfiguration : IEntityTypeConfiguration<MoneyOrderRecord>
{
    public void Configure(EntityTypeBuilder<MoneyOrderRecord> builder)
    {
        builder.ToTable("MoneyOrders", table =>
        {
            table.HasCheckConstraint("CK_MoneyOrders_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_MoneyOrders_Fee", "[Fee] >= 0");
        });
        builder.HasKey(order => order.Id).HasName("PK_MoneyOrders");
        builder.Property(order => order.Id).HasMaxLength(128).IsUnicode(false);
        builder.Property(order => order.Amount).HasPrecision(18, 2);
        builder.Property(order => order.SenderName).HasMaxLength(200).IsRequired();
        builder.Property(order => order.RecipientName).HasMaxLength(200).IsRequired();
        builder.Property(order => order.Fee).HasPrecision(18, 2);
        builder.Property(order => order.BranchCode).HasConversion(SqlMapping.NullableBranchCodeConverter).HasMaxLength(10).IsUnicode(false);
        builder.Property(order => order.TransactionId).HasMaxLength(128).IsUnicode(false);
        builder.Property(order => order.CreatedAtUtc).HasColumnType("datetime2(7)").HasConversion(SqlMapping.UtcDateTimeConverter);
        builder.HasIndex(order => order.TransactionId).IsUnique().HasFilter("[TransactionId] IS NOT NULL").HasDatabaseName("UX_MoneyOrders_Transaction");
        builder.HasIndex(order => new { order.BranchCode, order.CreatedAtUtc, order.Id }).IncludeProperties(order => new { order.Amount, order.Fee, order.TransactionId }).HasDatabaseName("IX_MoneyOrders_Branch_Created");
        builder.HasOne<Branch>().WithMany().HasForeignKey(order => order.BranchCode).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_MoneyOrders_Branches");
        builder.HasOne<CounterTransactionRecord>().WithMany().HasForeignKey(order => order.TransactionId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_MoneyOrders_CounterTransactions");
        builder.HasOne(order => order.IdentityVerification).WithOne().HasForeignKey<IdentityVerificationRecord>(verification => verification.MoneyOrderId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_IdentityVerifications_MoneyOrders");
        SqlMapping.AddAuditColumns(builder);
    }
}

public sealed class IdentityVerificationConfiguration : IEntityTypeConfiguration<IdentityVerificationRecord>
{
    public void Configure(EntityTypeBuilder<IdentityVerificationRecord> builder)
    {
        builder.ToTable("IdentityVerifications", table => table.HasCheckConstraint("CK_IdentityVerifications_MaskedNumber", "LEN([MaskedDocumentNumber]) = 8 AND LEFT([MaskedDocumentNumber], 4) = 'XXXX' AND SUBSTRING([MaskedDocumentNumber], 5, 4) NOT LIKE '%[^0-9A-Za-z]%'"));
        builder.HasKey(verification => verification.Id).HasName("PK_IdentityVerifications");
        builder.Property(verification => verification.Id).HasMaxLength(128).IsUnicode(false);
        builder.Property(verification => verification.MoneyOrderId).HasMaxLength(128).IsUnicode(false);
        builder.Property(verification => verification.DocumentType).HasMaxLength(100).IsRequired();
        builder.Property(verification => verification.MaskedDocumentNumber).HasMaxLength(8).IsUnicode(false).IsRequired();
        builder.Property(verification => verification.VerifiedAtUtc).HasColumnType("datetime2(7)").HasConversion(SqlMapping.UtcDateTimeConverter);
        SqlMapping.AddAuditColumns(builder);
    }
}

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovementRecord>
{
    public void Configure(EntityTypeBuilder<StockMovementRecord> builder)
    {
        builder.ToTable("StockMovements", table =>
        {
            table.HasCheckConstraint("CK_StockMovements_Delta", "[QuantityDelta] <> 0");
            table.HasCheckConstraint("CK_StockMovements_Quantities", "[QuantityBefore] >= 0 AND [QuantityAfter] >= 0");
        });
        builder.HasKey(movement => movement.Id).HasName("PK_StockMovements");
        builder.Property(movement => movement.Id).ValueGeneratedOnAdd();
        builder.Property(movement => movement.BranchCode).HasConversion(SqlMapping.BranchCodeConverter).HasMaxLength(10).IsUnicode(false);
        builder.Property(movement => movement.Sku).HasConversion(SqlMapping.SkuConverter).HasMaxLength(20).IsUnicode(false);
        builder.Property(movement => movement.MovementType).HasMaxLength(24).IsUnicode(false);
        builder.Property(movement => movement.TransactionId).HasMaxLength(128).IsUnicode(false);
        builder.Property(movement => movement.Reason).HasMaxLength(500);
        builder.Property(movement => movement.ChangedBy).HasMaxLength(128).IsUnicode(false);
        builder.HasOne<Branch>().WithMany().HasForeignKey(movement => movement.BranchCode).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_StockMovements_Branches");
        builder.HasOne<Product>().WithMany().HasForeignKey(movement => movement.Sku).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_StockMovements_Products");
        builder.HasOne<CounterTransactionRecord>().WithMany().HasForeignKey(movement => movement.TransactionId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_StockMovements_CounterTransactions");
        builder.HasIndex(movement => movement.TransactionId).IsUnique().HasFilter("[TransactionId] IS NOT NULL AND [MovementType] = 'ReversalRestore'").HasDatabaseName("UX_StockMovements_Reversal");
        SqlMapping.AddAuditColumns(builder);
    }
}

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntryRecord>
{
    public void Configure(EntityTypeBuilder<AuditEntryRecord> builder)
    {
        builder.ToTable("AuditEntries", table =>
        {
            table.HasCheckConstraint("CK_AuditEntries_StatusCode", "[StatusCode] >= 0");
            table.HasCheckConstraint("CK_AuditEntries_Duration", "[DurationMilliseconds] >= 0");
        });
        builder.HasKey(entry => entry.Id).HasName("PK_AuditEntries");
        builder.Property(entry => entry.Id).ValueGeneratedOnAdd();
        builder.Property(entry => entry.Timestamp).HasColumnType("datetime2(7)").HasConversion(SqlMapping.UtcDateTimeConverter);
        builder.Property(entry => entry.StaffId).HasMaxLength(128).IsUnicode(false);
        builder.Property(entry => entry.Role).HasMaxLength(64).IsUnicode(false);
        builder.Property(entry => entry.BranchCode).HasMaxLength(10).IsUnicode(false);
        builder.Property(entry => entry.Controller).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.Action).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.RouteValuesJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(entry => entry.CorrelationId).HasMaxLength(128).IsUnicode(false);
        builder.HasIndex(entry => new { entry.Timestamp, entry.BranchCode }).HasDatabaseName("IX_AuditEntries_Timestamp_Branch");
        SqlMapping.AddAuditColumns(builder);
    }
}

public sealed class ImportRunConfiguration : IEntityTypeConfiguration<ImportRunRecord>
{
    public void Configure(EntityTypeBuilder<ImportRunRecord> builder)
    {
        builder.ToTable("JsonImportRuns", table => table.HasCheckConstraint("CK_JsonImportRuns_Counts", "[SourceCount] >= 0 AND [ImportedCount] >= 0 AND [SkippedCount] >= 0"));
        builder.HasKey(run => run.Id).HasName("PK_JsonImportRuns");
        builder.Property(run => run.Id).ValueGeneratedOnAdd();
        builder.Property(run => run.SourceFingerprint).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.HasIndex(run => run.SourceFingerprint).IsUnique().HasDatabaseName("UX_JsonImportRuns_SourceFingerprint");
        builder.Property(run => run.ImportedAtUtc).HasColumnType("datetime2(7)").HasConversion(SqlMapping.UtcDateTimeConverter);
        builder.Property(run => run.ImporterVersion).HasMaxLength(32).IsUnicode(false);
        SqlMapping.AddAuditColumns(builder);
    }
}
