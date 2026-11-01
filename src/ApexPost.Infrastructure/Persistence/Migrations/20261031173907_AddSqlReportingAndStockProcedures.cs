using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSqlReportingAndStockProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_StockMovements_Reversal",
                table: "StockMovements");

            migrationBuilder.CreateIndex(
                name: "UX_StockMovements_Reversal",
                table: "StockMovements",
                column: "TransactionId",
                unique: true,
                filter: "[TransactionId] IS NOT NULL AND [MovementType] = 'ReversalRestore'");

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
                    @ReportDate date,
                    @BranchCode varchar(10) = NULL
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SELECT BranchCode, CONVERT(date, OccurredAtUtc) AS SaleDate,
                           COUNT_BIG(*) AS TransactionCount, SUM(Total) AS GrossSales
                    FROM dbo.CounterTransactions
                    WHERE Status = 'Paid' AND OccurredAtUtc >= @ReportDate
                      AND OccurredAtUtc < DATEADD(day, 1, @ReportDate)
                      AND (@BranchCode IS NULL OR BranchCode = @BranchCode)
                    GROUP BY BranchCode, CONVERT(date, OccurredAtUtc);
                END
                """);
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
                    @ReorderLevel int = 5,
                    @BranchCode varchar(10) = NULL
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SELECT stock.BranchCode, stock.Sku, product.Name AS ProductName,
                           stock.Quantity, @ReorderLevel AS ReorderLevel
                    FROM dbo.BranchStock AS stock
                    INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
                    WHERE stock.Quantity <= @ReorderLevel
                      AND (@BranchCode IS NULL OR stock.BranchCode = @BranchCode)
                    ORDER BY stock.BranchCode, stock.Quantity, stock.Sku;
                END
                """);
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
                    @BranchCode varchar(10),
                    @Sku varchar(20),
                    @Delta int,
                    @Reason nvarchar(500),
                    @ChangedBy varchar(128)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;
                    IF @Delta = 0 OR @Delta = -2147483648
                        THROW 51000, 'Stock adjustment must be a non-zero supported quantity.', 1;
                    IF NULLIF(LTRIM(RTRIM(@Reason)), '') IS NULL OR NULLIF(LTRIM(RTRIM(@ChangedBy)), '') IS NULL
                        THROW 51001, 'A reason and actor are required for stock adjustments.', 1;

                    BEGIN TRANSACTION;
                    DECLARE @Before int;
                    SELECT @Before = Quantity FROM dbo.BranchStock WITH (UPDLOCK, HOLDLOCK)
                    WHERE BranchCode = @BranchCode AND Sku = @Sku;
                    IF @Before IS NULL
                    BEGIN
                        IF @Delta < 0
                        BEGIN
                            ROLLBACK TRANSACTION;
                            THROW 51002, 'Cannot reduce stock when no stock row exists.', 1;
                        END;
                        SET @Before = 0;
                        INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedAtUtc, CreatedBy)
                        VALUES (@BranchCode, @Sku, @Delta, SYSUTCDATETIME(), @ChangedBy);
                    END
                    ELSE
                    BEGIN
                        IF CONVERT(bigint, @Before) + @Delta < 0
                        BEGIN
                            ROLLBACK TRANSACTION;
                            THROW 51003, 'Stock adjustment would make quantity negative.', 1;
                        END;
                        UPDATE dbo.BranchStock SET Quantity = @Before + @Delta,
                            ModifiedAtUtc = SYSUTCDATETIME()
                        WHERE BranchCode = @BranchCode AND Sku = @Sku;
                    END;

                    INSERT dbo.StockMovements
                        (BranchCode, Sku, QuantityDelta, QuantityBefore, QuantityAfter,
                         MovementType, Reason, ChangedBy, CreatedAtUtc)
                    VALUES
                        (@BranchCode, @Sku, @Delta, @Before, @Before + @Delta,
                         'Adjustment', @Reason, @ChangedBy, SYSUTCDATETIME());
                    COMMIT TRANSACTION;
                    SELECT BranchCode, Sku, Quantity FROM dbo.BranchStock
                    WHERE BranchCode = @BranchCode AND Sku = @Sku;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_AdjustStock;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_LowStockReport;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_DailyBranchSales;");
            migrationBuilder.DropIndex(
                name: "UX_StockMovements_Reversal",
                table: "StockMovements");

            migrationBuilder.CreateIndex(
                name: "UX_StockMovements_Reversal",
                table: "StockMovements",
                columns: new[] { "TransactionId", "MovementType" },
                unique: true,
                filter: "[TransactionId] IS NOT NULL AND [MovementType] IN ('VoidRestore','RefundRestore')");
        }
    }
}
