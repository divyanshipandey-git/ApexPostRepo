using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    StaffId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    Role = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    BranchCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Controller = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RouteValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                    table.CheckConstraint("CK_AuditEntries_Duration", "[DurationMilliseconds] >= 0");
                    table.CheckConstraint("CK_AuditEntries_StatusCode", "[StatusCode] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    BranchCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OpeningTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    ClosingTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.BranchCode);
                    table.CheckConstraint("CK_Branches_OpeningHours", "[ClosingTime] > [OpeningTime]");
                });

            migrationBuilder.CreateTable(
                name: "JsonImportRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceFingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ImporterVersion = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    SourceCount = table.Column<int>(type: "int", nullable: false),
                    ImportedCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JsonImportRuns", x => x.Id);
                    table.CheckConstraint("CK_JsonImportRuns_Counts", "[SourceCount] >= 0 AND [ImportedCount] >= 0 AND [SkippedCount] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    CategoryCode = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.CategoryCode);
                    table.CheckConstraint("CK_ProductCategories_Code", "[CategoryCode] IN ('Packaging','Stationery','OfficeSupplies')");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleCode = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleCode);
                    table.CheckConstraint("CK_Roles_Code", "[RoleCode] IN ('CounterClerk','BranchManager','Auditor','Administrator')");
                });

            migrationBuilder.CreateTable(
                name: "ShippingZones",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingZones", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Counters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    BranchCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Counters_Branches",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Sku = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxCategory = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Sku);
                    table.CheckConstraint("CK_Products_TaxCategory", "[TaxCategory] IN ('Standard','Reduced','ZeroRated','Exempt')");
                    table.CheckConstraint("CK_Products_UnitPrice", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_Products_ProductCategories",
                        column: x => x.Category,
                        principalTable: "ProductCategories",
                        principalColumn: "CategoryCode");
                });

            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Role = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    HomeBranchCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Staff_Branches",
                        column: x => x.HomeBranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode");
                    table.ForeignKey(
                        name: "FK_Staff_Roles",
                        column: x => x.Role,
                        principalTable: "Roles",
                        principalColumn: "RoleCode");
                });

            migrationBuilder.CreateTable(
                name: "ShippingRates",
                columns: table => new
                {
                    ZoneCode = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ServiceLevel = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PricePerKilogram = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingRates", x => new { x.ZoneCode, x.ServiceLevel });
                    table.CheckConstraint("CK_ShippingRates_Prices", "[BasePrice] >= 0 AND [PricePerKilogram] >= 0");
                    table.CheckConstraint("CK_ShippingRates_ServiceLevel", "[ServiceLevel] IN ('Standard','Express','Registered')");
                    table.ForeignKey(
                        name: "FK_ShippingRates_ShippingZones",
                        column: x => x.ZoneCode,
                        principalTable: "ShippingZones",
                        principalColumn: "Code");
                });

            migrationBuilder.CreateTable(
                name: "BranchStock",
                columns: table => new
                {
                    BranchCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Sku = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchStock", x => new { x.BranchCode, x.Sku });
                    table.CheckConstraint("CK_BranchStock_Quantity_NonNegative", "[Quantity] >= 0");
                    table.ForeignKey(
                        name: "FK_BranchStock_Branches",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode");
                    table.ForeignKey(
                        name: "FK_BranchStock_Products",
                        column: x => x.Sku,
                        principalTable: "Products",
                        principalColumn: "Sku");
                });

            migrationBuilder.CreateTable(
                name: "CounterTransactions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    BranchCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CounterId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    ClerkId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Tax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CounterTransactions", x => x.Id);
                    table.CheckConstraint("CK_CounterTransactions_Status", "[Status] IN ('Open','Paid','Voided','Refunded')");
                    table.CheckConstraint("CK_CounterTransactions_Totals", "[Subtotal] >= 0 AND [Tax] >= 0 AND [Total] >= 0");
                    table.ForeignKey(
                        name: "FK_CounterTransactions_Branches",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode");
                    table.ForeignKey(
                        name: "FK_CounterTransactions_Counters",
                        column: x => x.CounterId,
                        principalTable: "Counters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CounterTransactions_Staff",
                        column: x => x.ClerkId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StaffCredentials",
                columns: table => new
                {
                    StaffId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffCredentials", x => x.StaffId);
                    table.CheckConstraint("CK_StaffCredentials_Hash", "LEN([PasswordHash]) > 0");
                    table.ForeignKey(
                        name: "FK_StaffCredentials_Staff",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MoneyOrders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BranchCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    TransactionId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyOrders", x => x.Id);
                    table.CheckConstraint("CK_MoneyOrders_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_MoneyOrders_Fee", "[Fee] >= 0");
                    table.ForeignKey(
                        name: "FK_MoneyOrders_Branches",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode");
                    table.ForeignKey(
                        name: "FK_MoneyOrders_CounterTransactions",
                        column: x => x.TransactionId,
                        principalTable: "CounterTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    TransactionId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    Method = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.TransactionId);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Payments_Method", "[Method] IN ('Cash','Card','Upi')");
                    table.ForeignKey(
                        name: "FK_Payments_CounterTransactions",
                        column: x => x.TransactionId,
                        principalTable: "CounterTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    ItemType = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    LengthCm = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    WidthCm = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    HeightCm = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    ZoneCode = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ServiceLevel = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    BranchCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    CounterTransactionId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    CourierStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CourierStatusUpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.CheckConstraint("CK_Shipments_Dimensions", "[LengthCm] > 0 AND [WidthCm] > 0 AND [HeightCm] > 0");
                    table.CheckConstraint("CK_Shipments_ItemType", "[ItemType] IN ('Parcel','Letter')");
                    table.CheckConstraint("CK_Shipments_ServiceLevel", "[ServiceLevel] IN ('Standard','Express','Registered')");
                    table.CheckConstraint("CK_Shipments_Weight", "[WeightKg] >= 0");
                    table.ForeignKey(
                        name: "FK_Shipments_Branches",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode");
                    table.ForeignKey(
                        name: "FK_Shipments_CounterTransactions",
                        column: x => x.CounterTransactionId,
                        principalTable: "CounterTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Shipments_ShippingZones",
                        column: x => x.ZoneCode,
                        principalTable: "ShippingZones",
                        principalColumn: "Code");
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Sku = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    QuantityDelta = table.Column<int>(type: "int", nullable: false),
                    QuantityBefore = table.Column<int>(type: "int", nullable: false),
                    QuantityAfter = table.Column<int>(type: "int", nullable: false),
                    MovementType = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    TransactionId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_StockMovements_Delta", "[QuantityDelta] <> 0");
                    table.CheckConstraint("CK_StockMovements_Quantities", "[QuantityBefore] >= 0 AND [QuantityAfter] >= 0");
                    table.ForeignKey(
                        name: "FK_StockMovements_Branches",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode");
                    table.ForeignKey(
                        name: "FK_StockMovements_CounterTransactions",
                        column: x => x.TransactionId,
                        principalTable: "CounterTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockMovements_Products",
                        column: x => x.Sku,
                        principalTable: "Products",
                        principalColumn: "Sku");
                });

            migrationBuilder.CreateTable(
                name: "TransactionLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ItemType = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxCategory = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ReferenceId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionLines", x => x.Id);
                    table.CheckConstraint("CK_TransactionLines_Amounts", "[UnitPrice] >= 0 AND [LineTotal] >= 0");
                    table.CheckConstraint("CK_TransactionLines_ItemType", "[ItemType] IN ('Retail','Service')");
                    table.CheckConstraint("CK_TransactionLines_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_TransactionLines_TaxCategory", "[TaxCategory] IN ('Standard','Reduced','ZeroRated','Exempt')");
                    table.ForeignKey(
                        name: "FK_TransactionLines_CounterTransactions",
                        column: x => x.TransactionId,
                        principalTable: "CounterTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TransactionTaxRates",
                columns: table => new
                {
                    TransactionId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionTaxRates", x => new { x.TransactionId, x.Category });
                    table.CheckConstraint("CK_TransactionTaxRates_Rate", "[Rate] >= 0 AND [Rate] <= 1");
                    table.ForeignKey(
                        name: "FK_TransactionTaxRates_CounterTransactions",
                        column: x => x.TransactionId,
                        principalTable: "CounterTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "IdentityVerifications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    MoneyOrderId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaskedDocumentNumber = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityVerifications", x => x.Id);
                    table.CheckConstraint("CK_IdentityVerifications_MaskedNumber", "LEN([MaskedDocumentNumber]) = 8 AND LEFT([MaskedDocumentNumber], 4) = 'XXXX' AND SUBSTRING([MaskedDocumentNumber], 5, 4) NOT LIKE '%[^0-9A-Za-z]%'");
                    table.ForeignKey(
                        name: "FK_IdentityVerifications_MoneyOrders",
                        column: x => x.MoneyOrderId,
                        principalTable: "MoneyOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "ProductCategories",
                columns: new[] { "CategoryCode", "CreatedBy", "Name" },
                values: new object[,]
                {
                    { "OfficeSupplies", null, "Office Supplies" },
                    { "Packaging", null, "Packaging" },
                    { "Stationery", null, "Stationery" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "RoleCode", "CreatedBy", "Name" },
                values: new object[,]
                {
                    { "Administrator", null, "Administrator" },
                    { "Auditor", null, "Auditor" },
                    { "BranchManager", null, "Branch Manager" },
                    { "CounterClerk", null, "Counter Clerk" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Timestamp_Branch",
                table: "AuditEntries",
                columns: new[] { "Timestamp", "BranchCode" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchStock_Branch_Quantity_Sku",
                table: "BranchStock",
                columns: new[] { "BranchCode", "Quantity", "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchStock_Sku",
                table: "BranchStock",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_Counters_BranchCode",
                table: "Counters",
                column: "BranchCode");

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_Branch_Occurred_Status",
                table: "CounterTransactions",
                columns: new[] { "BranchCode", "OccurredAtUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_ClerkId",
                table: "CounterTransactions",
                column: "ClerkId");

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_CounterId",
                table: "CounterTransactions",
                column: "CounterId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityVerifications_MoneyOrderId",
                table: "IdentityVerifications",
                column: "MoneyOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_JsonImportRuns_SourceFingerprint",
                table: "JsonImportRuns",
                column: "SourceFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoneyOrders_Branch_Created",
                table: "MoneyOrders",
                columns: new[] { "BranchCode", "CreatedAtUtc", "Id" })
                .Annotation("SqlServer:Include", new[] { "Amount", "Fee", "TransactionId" });

            migrationBuilder.CreateIndex(
                name: "UX_MoneyOrders_Transaction",
                table: "MoneyOrders",
                column: "TransactionId",
                unique: true,
                filter: "[TransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Category",
                table: "Products",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_BranchCode",
                table: "Shipments",
                column: "BranchCode");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_CounterTransactionId",
                table: "Shipments",
                column: "CounterTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Zone_Service_Created",
                table: "Shipments",
                columns: new[] { "ZoneCode", "ServiceLevel", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Staff_HomeBranchCode",
                table: "Staff",
                column: "HomeBranchCode");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_Role",
                table: "Staff",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "UX_Staff_Email",
                table: "Staff",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_BranchCode",
                table: "StockMovements",
                column: "BranchCode");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Sku",
                table: "StockMovements",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "UX_StockMovements_Reversal",
                table: "StockMovements",
                columns: new[] { "TransactionId", "MovementType" },
                unique: true,
                filter: "[TransactionId] IS NOT NULL AND [MovementType] IN ('VoidRestore','RefundRestore')");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLines_Transaction_ItemType",
                table: "TransactionLines",
                columns: new[] { "TransactionId", "ItemType" })
                .Annotation("SqlServer:Include", new[] { "LineTotal", "Quantity" });

            migrationBuilder.CreateIndex(
                name: "UX_TransactionLines_Transaction_LineNumber",
                table: "TransactionLines",
                columns: new[] { "TransactionId", "LineNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "BranchStock");

            migrationBuilder.DropTable(
                name: "IdentityVerifications");

            migrationBuilder.DropTable(
                name: "JsonImportRuns");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropTable(
                name: "ShippingRates");

            migrationBuilder.DropTable(
                name: "StaffCredentials");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "TransactionLines");

            migrationBuilder.DropTable(
                name: "TransactionTaxRates");

            migrationBuilder.DropTable(
                name: "MoneyOrders");

            migrationBuilder.DropTable(
                name: "ShippingZones");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "CounterTransactions");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.DropTable(
                name: "Counters");

            migrationBuilder.DropTable(
                name: "Staff");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
