-- Disposable-catalog demonstration of one atomic counter sale.
-- Default behaviour is rollback-only. The database guard prevents accidental
-- execution against ApexPostDb or any catalog outside the SQL integration prefix.
IF DB_NAME() NOT LIKE N'ApexPost_SqlIT[_]%'
    THROW 51100, 'Run this demonstration only in an ApexPost_SqlIT_* catalog.', 1;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @BranchCode varchar(10) = 'TEST01';
DECLARE @CounterId varchar(128) = 'TEST-COUNTER';
DECLARE @ClerkId varchar(128) = 'TEST-CLERK';
DECLARE @Sku varchar(20) = 'TESTSKU';
DECLARE @Quantity int = 1;
DECLARE @TaxRate decimal(9, 6) = 0.18;
DECLARE @CommitDemo bit = 0; -- Keep 0 to roll back the demonstration.

DECLARE @TransactionId varchar(128) = CONCAT('DEMO-', CONVERT(varchar(32), NEWID()));
DECLARE @UnitPrice decimal(18, 2);
DECLARE @TaxCategory varchar(32);
DECLARE @QuantityBefore int;
DECLARE @LineTotal decimal(18, 2);
DECLARE @Tax decimal(18, 2);
DECLARE @Total decimal(18, 2);
DECLARE @NowUtc datetime2(7) = SYSUTCDATETIME();

IF @Quantity <= 0 OR @TaxRate < 0 OR @TaxRate > 1
    THROW 51101, 'Quantity or demonstration tax rate is invalid.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    SELECT @UnitPrice = product.UnitPrice,
           @TaxCategory = product.TaxCategory
    FROM dbo.Products AS product
    WHERE product.Sku = @Sku;
    IF @UnitPrice IS NULL
        THROW 51102, 'The demonstration SKU does not exist.', 1;

    SELECT @QuantityBefore = stock.Quantity
    FROM dbo.BranchStock AS stock WITH (UPDLOCK, HOLDLOCK)
    WHERE stock.BranchCode = @BranchCode
      AND stock.Sku = @Sku;
    IF @QuantityBefore IS NULL OR @QuantityBefore < @Quantity
        THROW 51103, 'The branch does not have enough demonstration stock.', 1;

    SET @LineTotal = @UnitPrice * @Quantity;
    SET @Tax = ROUND(@LineTotal * @TaxRate, 2);
    SET @Total = @LineTotal + @Tax;

    INSERT dbo.CounterTransactions
        (Id, BranchCode, CounterId, ClerkId, Status, OccurredAtUtc, Subtotal, Tax, Total)
    VALUES
        (@TransactionId, @BranchCode, @CounterId, @ClerkId, 'Paid', @NowUtc, @LineTotal, @Tax, @Total);

    INSERT dbo.TransactionTaxRates (TransactionId, Category, Rate)
    VALUES (@TransactionId, @TaxCategory, @TaxRate);

    INSERT dbo.TransactionLines
        (TransactionId, LineNumber, Description, ItemType, Quantity, UnitPrice, TaxCategory, ReferenceId, LineTotal)
    VALUES
        (@TransactionId, 1, CONCAT('Demonstration sale: ', @Sku), 'Retail', @Quantity,
         @UnitPrice, @TaxCategory, @Sku, @LineTotal);

    INSERT dbo.Payments (TransactionId, Method, Amount, PaidAtUtc)
    VALUES (@TransactionId, 'Cash', @Total, @NowUtc);

    UPDATE dbo.BranchStock
    SET Quantity = Quantity - @Quantity,
        ModifiedAtUtc = @NowUtc
    WHERE BranchCode = @BranchCode
      AND Sku = @Sku
      AND Quantity >= @Quantity;

    IF @@ROWCOUNT <> 1
        THROW 51104, 'Stock changed before the sale could be completed.', 1;

    INSERT dbo.StockMovements
        (BranchCode, Sku, QuantityDelta, QuantityBefore, QuantityAfter,
         MovementType, TransactionId, Reason, ChangedBy, CreatedAtUtc)
    VALUES
        (@BranchCode, @Sku, -@Quantity, @QuantityBefore, @QuantityBefore - @Quantity,
         'Sale', @TransactionId, N'Rollback-only counter-sale demonstration', @ClerkId, @NowUtc);

    IF @CommitDemo = 1
        COMMIT TRANSACTION;
    ELSE
        ROLLBACK TRANSACTION;

    SELECT @TransactionId AS DemonstrationTransactionId,
           @LineTotal AS Subtotal,
           @Tax AS Tax,
           @Total AS Total,
           @CommitDemo AS ChangesCommitted;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
