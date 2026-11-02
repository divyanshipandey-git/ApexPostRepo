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
END;
