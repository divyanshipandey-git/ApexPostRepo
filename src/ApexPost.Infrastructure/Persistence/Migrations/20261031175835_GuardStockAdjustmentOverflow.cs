using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GuardStockAdjustmentOverflow : Migration
    {
        /// <inheritdoc />
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
                        IF CONVERT(bigint, @Before) + @Delta > 2147483647
                        BEGIN
                            ROLLBACK TRANSACTION;
                            THROW 51004, 'Stock adjustment exceeds the supported quantity range.', 1;
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
        // Keep the stronger overflow guard when rolling back unrelated migrations.
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
