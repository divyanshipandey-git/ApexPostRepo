using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApexPostDbContext))]
[Migration("20261103000000_HardenReportingProcedures")]
public partial class HardenReportingProcedures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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

                IF NULLIF(LTRIM(RTRIM(@BranchCode)), '') IS NULL
                   OR NULLIF(LTRIM(RTRIM(@Sku)), '') IS NULL
                    THROW 51009, 'Branch code and SKU are required.', 1;
                IF @Delta IS NULL OR @Delta = 0 OR @Delta = -2147483648
                    THROW 51000, 'Stock adjustment must be a non-zero supported quantity.', 1;
                IF NULLIF(LTRIM(RTRIM(@Reason)), '') IS NULL
                   OR NULLIF(LTRIM(RTRIM(@ChangedBy)), '') IS NULL
                    THROW 51001, 'A reason and actor are required for stock adjustments.', 1;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    DECLARE @Before int;
                    SELECT @Before = Quantity
                    FROM dbo.BranchStock WITH (UPDLOCK, HOLDLOCK)
                    WHERE BranchCode = @BranchCode AND Sku = @Sku;

                    IF @Before IS NULL
                    BEGIN
                        IF @Delta < 0
                            THROW 51002, 'Cannot reduce stock when no stock row exists.', 1;

                        SET @Before = 0;
                        INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedAtUtc, CreatedBy)
                        VALUES (@BranchCode, @Sku, @Delta, SYSUTCDATETIME(), @ChangedBy);
                    END
                    ELSE
                    BEGIN
                        IF CONVERT(bigint, @Before) + @Delta < 0
                            THROW 51003, 'Stock adjustment would make quantity negative.', 1;
                        IF CONVERT(bigint, @Before) + @Delta > 2147483647
                            THROW 51004, 'Stock adjustment exceeds the supported quantity range.', 1;

                        UPDATE dbo.BranchStock
                        SET Quantity = @Before + @Delta,
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
                    SELECT BranchCode, Sku, Quantity
                    FROM dbo.BranchStock
                    WHERE BranchCode = @BranchCode AND Sku = @Sku;
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
            END
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
                @BranchCode varchar(10) = NULL,
                @Threshold int = 5
            AS
            BEGIN
                SET NOCOUNT ON;

                IF @Threshold IS NULL OR @Threshold < 0 OR @Threshold > 100000
                    THROW 51007, 'Threshold must be between 0 and 100000.', 1;
                IF @BranchCode IS NOT NULL AND NULLIF(LTRIM(RTRIM(@BranchCode)), '') IS NULL
                    THROW 51008, 'Branch code cannot be blank.', 1;

                IF @BranchCode IS NULL
                BEGIN
                    SELECT stock.BranchCode, stock.Sku, product.Name AS ProductName,
                           stock.Quantity, @Threshold AS Threshold
                    FROM dbo.BranchStock AS stock
                    INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
                    WHERE stock.Quantity <= @Threshold
                    ORDER BY stock.BranchCode, stock.Quantity, stock.Sku;
                END
                ELSE
                BEGIN
                    SET @BranchCode = LTRIM(RTRIM(@BranchCode));

                    SELECT stock.BranchCode, stock.Sku, product.Name AS ProductName,
                           stock.Quantity, @Threshold AS Threshold
                    FROM dbo.BranchStock AS stock
                    INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
                    WHERE stock.BranchCode = @BranchCode
                      AND stock.Quantity <= @Threshold
                    ORDER BY stock.BranchCode, stock.Quantity, stock.Sku;
                END;
            END
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
                @BranchCode varchar(10) = NULL,
                @Date date
            AS
            BEGIN
                SET NOCOUNT ON;

                IF @Date IS NULL
                    THROW 51005, 'A report date is required.', 1;
                IF @BranchCode IS NOT NULL AND NULLIF(LTRIM(RTRIM(@BranchCode)), '') IS NULL
                    THROW 51006, 'Branch code cannot be blank.', 1;

                IF @BranchCode IS NOT NULL
                BEGIN
                    SET @BranchCode = LTRIM(RTRIM(@BranchCode));
                    IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE BranchCode = @BranchCode)
                        THROW 51010, 'The requested branch does not exist.', 1;
                END;

                DECLARE @StartUtc datetime2(7) = CONVERT(datetime2(7), @Date);
                DECLARE @EndUtc datetime2(7) = DATEADD(day, 1, @StartUtc);

                ;WITH DailySales AS
                (
                    SELECT transactionRow.BranchCode,
                           COUNT_BIG(DISTINCT transactionRow.Id) AS TransactionCount,
                           SUM(CASE WHEN line.ItemType = 'Retail'
                                THEN line.LineTotal + line.LineTotal * tax.Rate ELSE 0 END) AS RetailSales,
                           SUM(CASE WHEN line.ItemType = 'Service'
                                THEN line.LineTotal + line.LineTotal * tax.Rate ELSE 0 END) AS ServiceSales
                    FROM dbo.CounterTransactions AS transactionRow
                    INNER JOIN dbo.TransactionLines AS line
                        ON line.TransactionId = transactionRow.Id
                    INNER JOIN dbo.TransactionTaxRates AS tax
                        ON tax.TransactionId = line.TransactionId
                       AND tax.Category = line.TaxCategory
                    WHERE transactionRow.Status = 'Paid'
                      AND transactionRow.OccurredAtUtc >= @StartUtc
                      AND transactionRow.OccurredAtUtc < @EndUtc
                      AND line.ItemType IN ('Retail', 'Service')
                      AND (@BranchCode IS NULL OR transactionRow.BranchCode = @BranchCode)
                    GROUP BY transactionRow.BranchCode
                )
                SELECT branch.BranchCode,
                       @Date AS SaleDate,
                       COALESCE(sales.TransactionCount, 0) AS TransactionCount,
                       COALESCE(sales.RetailSales, 0) AS RetailSales,
                       COALESCE(sales.ServiceSales, 0) AS ServiceSales,
                       COALESCE(sales.RetailSales, 0) + COALESCE(sales.ServiceSales, 0) AS TotalSales
                FROM dbo.Branches AS branch
                LEFT JOIN DailySales AS sales ON sales.BranchCode = branch.BranchCode
                WHERE @BranchCode IS NULL OR branch.BranchCode = @BranchCode
                ORDER BY branch.BranchCode;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
                @BranchCode varchar(10) = NULL,
                @Date date
            AS
            BEGIN
                SET NOCOUNT ON;
                IF @Date IS NULL
                    THROW 51005, 'A report date is required.', 1;
                IF @BranchCode IS NOT NULL AND NULLIF(LTRIM(RTRIM(@BranchCode)), '') IS NULL
                    THROW 51006, 'Branch code cannot be blank.', 1;

                SELECT transactionRow.BranchCode,
                       CONVERT(date, transactionRow.OccurredAtUtc) AS SaleDate,
                       COUNT_BIG(DISTINCT transactionRow.Id) AS TransactionCount,
                       COALESCE(SUM(CASE WHEN line.ItemType = 'Retail'
                            THEN line.LineTotal + line.LineTotal * tax.Rate ELSE 0 END), 0) AS RetailSales,
                       COALESCE(SUM(CASE WHEN line.ItemType = 'Service'
                            THEN line.LineTotal + line.LineTotal * tax.Rate ELSE 0 END), 0) AS ServiceSales,
                       COALESCE(SUM(CASE WHEN line.ItemType IN ('Retail', 'Service')
                            THEN line.LineTotal + line.LineTotal * tax.Rate ELSE 0 END), 0) AS TotalSales
                FROM dbo.CounterTransactions AS transactionRow
                INNER JOIN dbo.TransactionLines AS line
                    ON line.TransactionId = transactionRow.Id
                INNER JOIN dbo.TransactionTaxRates AS tax
                    ON tax.TransactionId = line.TransactionId
                   AND tax.Category = line.TaxCategory
                WHERE transactionRow.Status = 'Paid'
                  AND transactionRow.OccurredAtUtc >= @Date
                  AND transactionRow.OccurredAtUtc < DATEADD(day, 1, @Date)
                  AND line.ItemType IN ('Retail', 'Service')
                  AND (@BranchCode IS NULL OR transactionRow.BranchCode = @BranchCode)
                GROUP BY transactionRow.BranchCode, CONVERT(date, transactionRow.OccurredAtUtc);
            END
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
                @BranchCode varchar(10) = NULL,
                @Threshold int = 5
            AS
            BEGIN
                SET NOCOUNT ON;
                IF @Threshold IS NULL OR @Threshold < 0 OR @Threshold > 100000
                    THROW 51007, 'Threshold must be between 0 and 100000.', 1;
                IF @BranchCode IS NOT NULL AND NULLIF(LTRIM(RTRIM(@BranchCode)), '') IS NULL
                    THROW 51008, 'Branch code cannot be blank.', 1;

                SELECT stock.BranchCode, stock.Sku, product.Name AS ProductName,
                       stock.Quantity, @Threshold AS Threshold
                FROM dbo.BranchStock AS stock
                INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
                WHERE stock.Quantity <= @Threshold
                  AND (@BranchCode IS NULL OR stock.BranchCode = @BranchCode)
                ORDER BY stock.BranchCode, stock.Quantity, stock.Sku
                OPTION (RECOMPILE);
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
                IF NULLIF(LTRIM(RTRIM(@BranchCode)), '') IS NULL
                   OR NULLIF(LTRIM(RTRIM(@Sku)), '') IS NULL
                    THROW 51009, 'Branch code and SKU are required.', 1;
                IF @Delta = 0 OR @Delta = -2147483648
                    THROW 51000, 'Stock adjustment must be a non-zero supported quantity.', 1;
                IF NULLIF(LTRIM(RTRIM(@Reason)), '') IS NULL
                   OR NULLIF(LTRIM(RTRIM(@ChangedBy)), '') IS NULL
                    THROW 51001, 'A reason and actor are required for stock adjustments.', 1;

                BEGIN TRY
                    BEGIN TRANSACTION;
                    DECLARE @Before int;
                    SELECT @Before = Quantity
                    FROM dbo.BranchStock WITH (UPDLOCK, HOLDLOCK)
                    WHERE BranchCode = @BranchCode AND Sku = @Sku;
                    IF @Before IS NULL
                    BEGIN
                        IF @Delta < 0
                            THROW 51002, 'Cannot reduce stock when no stock row exists.', 1;
                        SET @Before = 0;
                        INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedAtUtc, CreatedBy)
                        VALUES (@BranchCode, @Sku, @Delta, SYSUTCDATETIME(), @ChangedBy);
                    END
                    ELSE
                    BEGIN
                        IF CONVERT(bigint, @Before) + @Delta < 0
                            THROW 51003, 'Stock adjustment would make quantity negative.', 1;
                        IF CONVERT(bigint, @Before) + @Delta > 2147483647
                            THROW 51004, 'Stock adjustment exceeds the supported quantity range.', 1;
                        UPDATE dbo.BranchStock
                        SET Quantity = @Before + @Delta, ModifiedAtUtc = SYSUTCDATETIME()
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
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
            END
            """);
    }
}
