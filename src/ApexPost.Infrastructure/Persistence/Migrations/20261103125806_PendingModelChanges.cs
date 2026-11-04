using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipments_Dimensions",
                table: "Shipments");

            migrationBuilder.CreateTable(
                name: "DailyBranchSalesProcedureRow",
                columns: table => new
                {
                    BranchCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SaleDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionCount = table.Column<long>(type: "bigint", nullable: false),
                    RetailSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ServiceSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "LowStockProcedureRow",
                columns: table => new
                {
                    BranchCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ReorderLevel = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "TopProductRevenueProcedureRow",
                columns: table => new
                {
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegionRank = table.Column<int>(type: "int", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyBranchSalesProcedureRow");

            migrationBuilder.DropTable(
                name: "LowStockProcedureRow");

            migrationBuilder.DropTable(
                name: "TopProductRevenueProcedureRow");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipments_Dimensions",
                table: "Shipments",
                sql: "[LengthCm] > 0 AND [WidthCm] > 0 AND [HeightCm] > 0");
        }
    }
}
