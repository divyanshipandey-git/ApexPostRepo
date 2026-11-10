-- Standalone T-SQL Sale Transaction Script
-- Demonstrates a complete counter sale transaction with proper error handling
-- using BEGIN TRAN, TRY...CATCH, XACT_STATE() and THROW

-- This script simulates a complete sale: reserving stock, creating transaction,
-- lines, payment, and optional shipment/money order - all in one atomic transaction.
-- Replace parameter values with actual data when executing.

DECLARE
    @TransactionId varchar(128) = 'TXN-DEMO-' + CONVERT(varchar(32), NEWID()),
    @BranchCode varchar(10) = 'IND01',
    @CounterId varchar(128) = 'CTR-001',
    @ClerkId varchar(128) = 'staff-test-1',
    @PaymentMethod varchar(16) = 'Cash',
    @OccurredAtUtc datetime2(7) = SYSUTCDATETIME(),
    -- Example product lines (SKU, Quantity, UnitPrice, TaxCategory, ReferenceId)
    @ProductLines TABLE (
        Sku varchar(20),
        Quantity int,
        UnitPrice decimal(18,2),
        TaxCategory varchar(32),
        ReferenceId varchar(128)
    ),
    -- Optional shipment
    @ShipmentId varchar(128) = NULL,
    @ShipmentItemType varchar(16) = NULL,
    @ShipmentWeightKg decimal(9,3) = NULL,
    @ShipmentLengthCm decimal(9,2) = NULL,
    @ShipmentWidthCm decimal(9,2) = NULL,
    @ShipmentHeightCm decimal(9,2) = NULL,
    @ShipmentZoneCode varchar(32) = NULL,
    @ShipmentServiceLevel varchar(32) = NULL,
    -- Optional money order
    @MoneyOrderId varchar(128) = NULL,
    @MoneyOrderAmount decimal(18,2) = NULL,
    @MoneyOrderSenderName nvarchar(200) = NULL,
    @MoneyOrderRecipientName nvarchar(200) = NULL,
    @MoneyOrderFee decimal(18,2) = NULL,
    @MoneyOrderBranchCode varchar(10) = NULL,
    @MoneyOrderVerificationId varchar(128) = NULL,
    @MoneyOrderDocumentType nvarchar(100) = NULL,
    @MoneyOrderMaskedNumber varchar(8) = NULL,
    @MoneyOrderIsVerified bit = NULL;

-- Example: Add product lines (replace with actual data)
INSERT INTO @ProductLines (Sku, Quantity, UnitPrice, TaxCategory, ReferenceId)
VALUES
    ('ENV001', 2, 1.50, 'Standard', 'ENV001'),
    ('BOX001', 1, 5.00, 'Standard', 'BOX001');

-- Uncomment and populate for shipment
/*
SET @ShipmentId = 'SHP-' + CONVERT(varchar(32), NEWID());
SET @ShipmentItemType = 'Parcel';
SET @ShipmentWeightKg = 1.5;
SET @ShipmentLengthCm = 20;
SET @ShipmentWidthCm = 15;
SET @ShipmentHeightCm = 10;
SET @ShipmentZoneCode = 'ZONE1';
SET @ShipmentServiceLevel = 'Standard';
*/

-- Uncomment and populate for money order
/*
SET @MoneyOrderId = 'MO-' + CONVERT(varchar(32), NEWID());
SET @MoneyOrderAmount = 100.00;
SET @MoneyOrderSenderName = N'John Doe';
SET @MoneyOrderRecipientName = N'Jane Smith';
SET @MoneyOrderFee = 10.00;
SET @MoneyOrderBranchCode = @BranchCode;
*/

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. Validate branch, counter, clerk exist and are active
    IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE BranchCode = @BranchCode AND IsActive = 1)
        THROW 50001, 'Branch not found or inactive.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Counters WHERE Id = @CounterId AND BranchCode = @BranchCode AND IsActive = 1)
        THROW 50002, 'Counter not found or inactive.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Staff WHERE Id = @ClerkId AND HomeBranchCode = @BranchCode AND IsActive = 1)
        THROW 50003, 'Clerk not found or inactive.', 1;

    IF @PaymentMethod NOT IN ('Cash', 'Card', 'Upi')
        THROW 50004, 'Invalid payment method.', 1;

    -- 2. Validate and reserve stock for retail products
    DECLARE @StockReservations TABLE (Sku varchar(20), ReservedQuantity int, BeforeQuantity int);
    DECLARE @Sku varchar(20), @Quantity int, @UnitPrice decimal(18,2), @TaxCategory varchar(32), @ReferenceId varchar(128);

    DECLARE product_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT Sku, Quantity, UnitPrice, TaxCategory, ReferenceId FROM @ProductLines;

    OPEN product_cursor;
    FETCH NEXT FROM product_cursor INTO @Sku, @Quantity, @UnitPrice, @TaxCategory, @ReferenceId;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @CurrentStock int;
        SELECT @CurrentStock = Quantity
        FROM dbo.BranchStock WITH (UPDLOCK, HOLDLOCK)
        WHERE BranchCode = @BranchCode AND Sku = @Sku;

        IF @CurrentStock IS NULL
        BEGIN
            IF @Quantity > 0
                THROW 50005, 'Product not stocked at this branch: ' + @Sku, 1;
            SET @CurrentStock = 0;
        END

        IF @CurrentStock < @Quantity
            THROW 50006, 'Insufficient stock for SKU ' + @Sku + '. Available: ' + CAST(@CurrentStock AS varchar(10)), 1;

        -- Reserve stock (decrement immediately within transaction)
        UPDATE dbo.BranchStock
        SET Quantity = @CurrentStock - @Quantity,
            ModifiedAtUtc = SYSUTCDATETIME()
        WHERE BranchCode = @BranchCode AND Sku = @Sku;

        INSERT INTO @StockReservations (Sku, ReservedQuantity, BeforeQuantity)
        VALUES (@Sku, @Quantity, @CurrentStock);

        -- Record stock movement
        INSERT INTO dbo.StockMovements
            (BranchCode, Sku, QuantityDelta, QuantityBefore, QuantityAfter,
             MovementType, Reason, ChangedBy, CreatedAtUtc)
        VALUES
            (@BranchCode, @Sku, -@Quantity, @CurrentStock, @CurrentStock - @Quantity,
             'Sale', 'Counter sale ' + @TransactionId, @ClerkId, SYSUTCDATETIME());

        FETCH NEXT FROM product_cursor INTO @Sku, @Quantity, @UnitPrice, @TaxCategory, @ReferenceId;
    END
    CLOSE product_cursor;
    DEALLOCATE product_cursor;

    -- 3. Calculate totals
    DECLARE @Subtotal decimal(18,2) = 0, @TaxTotal decimal(18,2) = 0;
    DECLARE @LineNumber int = 1;

    -- Get tax rates (simplified - in reality would come from configuration)
    DECLARE @TaxRates TABLE (Category varchar(32), Rate decimal(9,6));
    INSERT INTO @TaxRates (Category, Rate)
    VALUES ('Standard', 0.20), ('Reduced', 0.05), ('ZeroRated', 0.00), ('Exempt', 0.00);

    -- Calculate subtotal from product lines
    SELECT @Subtotal = SUM(pl.Quantity * pl.UnitPrice) FROM @ProductLines pl;

    -- Add shipment price if applicable
    DECLARE @ShippingPrice decimal(18,2) = 0;
    IF @ShipmentId IS NOT NULL
    BEGIN
        DECLARE @BasePrice decimal(18,2), @PricePerKg decimal(18,2);
        SELECT @BasePrice = BasePrice, @PricePerKg = PricePerKilogram
        FROM dbo.ShippingRates
        WHERE ZoneCode = @ShipmentZoneCode AND ServiceLevel = @ShipmentServiceLevel;

        IF @BasePrice IS NULL
            THROW 50007, 'Shipping rate not found for zone ' + @ShipmentZoneCode + ' and service level ' + @ShipmentServiceLevel, 1;

        SET @ShippingPrice = @BasePrice + @PricePerKg * @ShipmentWeightKg;
        SET @Subtotal = @Subtotal + @ShippingPrice;
    END

    -- Add money order total if applicable
    DECLARE @MoneyOrderTotal decimal(18,2) = 0;
    IF @MoneyOrderId IS NOT NULL
    BEGIN
        SET @MoneyOrderTotal = @MoneyOrderAmount + @MoneyOrderFee;
        SET @Subtotal = @Subtotal + @MoneyOrderTotal;
    END

    -- Calculate tax per line
    DECLARE line_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT pl.Sku, pl.Quantity, pl.UnitPrice, pl.TaxCategory, pl.ReferenceId
        FROM @ProductLines pl;

    OPEN line_cursor;
    FETCH NEXT FROM line_cursor INTO @Sku, @Quantity, @UnitPrice, @TaxCategory, @ReferenceId;

    DECLARE @LineTotal decimal(18,2), @TaxRate decimal(9,6);

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @LineTotal = @Quantity * @UnitPrice;
        SELECT @TaxRate = Rate FROM @TaxRates WHERE Category = @TaxCategory;
        SET @TaxTotal = @TaxTotal + (@LineTotal * @TaxRate);

        FETCH NEXT FROM line_cursor INTO @Sku, @Quantity, @UnitPrice, @TaxCategory, @ReferenceId;
    END
    CLOSE line_cursor;
    DEALLOCATE line_cursor;

    -- Shipping tax (Standard)
    IF @ShipmentId IS NOT NULL
    BEGIN
        SELECT @TaxRate = Rate FROM @TaxRates WHERE Category = 'Standard';
        SET @TaxTotal = @TaxTotal + (@ShippingPrice * @TaxRate);
    END

    -- Money order tax (Exempt)
    -- No tax added for money orders

    DECLARE @Total decimal(18,2) = @Subtotal + @TaxTotal;

    -- 4. Insert CounterTransaction
    INSERT INTO dbo.CounterTransactions
        (Id, BranchCode, CounterId, ClerkId, Status, OccurredAtUtc, Subtotal, Tax, Total, RowVersion, CreatedAtUtc, CreatedBy)
    VALUES
        (@TransactionId, @BranchCode, @CounterId, @ClerkId, 'Paid', @OccurredAtUtc, @Subtotal, @TaxTotal, @Total, 0, SYSUTCDATETIME(), @ClerkId);

    -- 5. Insert TransactionLines for retail products
    SET @LineNumber = 1;
    DECLARE line_cursor2 CURSOR LOCAL FAST_FORWARD FOR
        SELECT pl.Sku, pl.Quantity, pl.UnitPrice, pl.TaxCategory, pl.ReferenceId
        FROM @ProductLines pl;

    OPEN line_cursor2;
    FETCH NEXT FROM line_cursor2 INTO @Sku, @Quantity, @UnitPrice, @TaxCategory, @ReferenceId;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @LineTotal = @Quantity * @UnitPrice;
        SELECT @TaxRate = Rate FROM @TaxRates WHERE Category = @TaxCategory;

        INSERT INTO dbo.TransactionLines
            (TransactionId, LineNumber, Description, ItemType, Quantity, UnitPrice, TaxCategory, ReferenceId, LineTotal, CreatedAtUtc, CreatedBy)
        VALUES
            (@TransactionId, @LineNumber,
             (SELECT Name FROM dbo.Products WHERE Sku = @Sku),
             'Retail', @Quantity, @UnitPrice, @TaxCategory, @ReferenceId, @LineTotal, SYSUTCDATETIME(), @ClerkId);

        INSERT INTO dbo.TransactionTaxRates
            (TransactionId, Category, Rate, CreatedAtUtc, CreatedBy)
        VALUES
            (@TransactionId, @TaxCategory, @TaxRate, SYSUTCDATETIME(), @ClerkId);

        SET @LineNumber = @LineNumber + 1;
        FETCH NEXT FROM line_cursor2 INTO @Sku, @Quantity, @UnitPrice, @TaxCategory, @ReferenceId;
    END
    CLOSE line_cursor2;
    DEALLOCATE line_cursor2;

    -- 6. Insert TransactionLine for shipment
    IF @ShipmentId IS NOT NULL
    BEGIN
        SELECT @TaxRate = Rate FROM @TaxRates WHERE Category = 'Standard';

        INSERT INTO dbo.TransactionLines
            (TransactionId, LineNumber, Description, ItemType, Quantity, UnitPrice, TaxCategory, ReferenceId, LineTotal, CreatedAtUtc, CreatedBy)
        VALUES
            (@TransactionId, @LineNumber,
             'Shipping (' + @ShipmentItemType + ', ' + @ShipmentZoneCode + ', ' + @ShipmentServiceLevel + ', ' + @ShipmentId + ')',
             'Service', 1, @ShippingPrice, 'Standard', @ShipmentId, @ShippingPrice, SYSUTCDATETIME(), @ClerkId);

        INSERT INTO dbo.TransactionTaxRates
            (TransactionId, Category, Rate, CreatedAtUtc, CreatedBy)
        VALUES
            (@TransactionId, 'Standard', @TaxRate, SYSUTCDATETIME(), @ClerkId);

        -- Insert Shipment record
        INSERT INTO dbo.Shipments
            (Id, ItemType, WeightKg, LengthCm, WidthCm, HeightCm, ZoneCode, ServiceLevel, BranchCode, CounterTransactionId, CreatedAtUtc, CreatedBy)
        VALUES
            (@ShipmentId, @ShipmentItemType, @ShipmentWeightKg, @ShipmentLengthCm, @ShipmentWidthCm, @ShipmentHeightCm,
             @ShipmentZoneCode, @ShipmentServiceLevel, @BranchCode, @TransactionId, SYSUTCDATETIME(), @ClerkId);

        SET @LineNumber = @LineNumber + 1;
    END

    -- 7. Insert TransactionLine for money order
    IF @MoneyOrderId IS NOT NULL
    BEGIN
        SELECT @TaxRate = Rate FROM @TaxRates WHERE Category = 'Exempt';

        INSERT INTO dbo.TransactionLines
            (TransactionId, LineNumber, Description, ItemType, Quantity, UnitPrice, TaxCategory, ReferenceId, LineTotal, CreatedAtUtc, CreatedBy)
        VALUES
            (@TransactionId, @LineNumber,
             'Money order ' + @MoneyOrderId + ' for ' + @MoneyOrderRecipientName,
             'Service', 1, @MoneyOrderTotal, 'Exempt', NULL, @MoneyOrderTotal, SYSUTCDATETIME(), @ClerkId);

        INSERT INTO dbo.TransactionTaxRates
            (TransactionId, Category, Rate, CreatedAtUtc, CreatedBy)
        VALUES
            (@TransactionId, 'Exempt', @TaxRate, SYSUTCDATETIME(), @ClerkId);

        -- Insert MoneyOrder record
        INSERT INTO dbo.MoneyOrders
            (Id, Amount, SenderName, RecipientName, Fee, BranchCode, TransactionId, CreatedAtUtc, CreatedBy)
        VALUES
            (@MoneyOrderId, @MoneyOrderAmount, @MoneyOrderSenderName, @MoneyOrderRecipientName,
             @MoneyOrderFee, @MoneyOrderBranchCode, @TransactionId, SYSUTCDATETIME(), @ClerkId);

        -- Insert IdentityVerification if provided
        IF @MoneyOrderVerificationId IS NOT NULL
        BEGIN
            INSERT INTO dbo.IdentityVerifications
                (Id, MoneyOrderId, DocumentType, MaskedDocumentNumber, IsVerified, VerifiedAtUtc, CreatedAtUtc, CreatedBy)
            VALUES
                (@MoneyOrderVerificationId, @MoneyOrderId, @MoneyOrderDocumentType, @MoneyOrderMaskedNumber,
                 @MoneyOrderIsVerified, SYSUTCDATETIME(), SYSUTCDATETIME(), @ClerkId);
        END

        SET @LineNumber = @LineNumber + 1;
    END

    -- 8. Insert Payment record
    INSERT INTO dbo.Payments
        (TransactionId, Method, Amount, PaidAtUtc, CreatedAtUtc, CreatedBy)
    VALUES
        (@TransactionId, @PaymentMethod, @Total, @OccurredAtUtc, SYSUTCDATETIME(), @ClerkId);

    COMMIT TRANSACTION;

    -- Return the created transaction ID
    SELECT @TransactionId AS CreatedTransactionId;

END TRY
BEGIN CATCH
    -- Rollback if transaction is still active
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    -- Re-throw the error with original error number and message
    DECLARE @ErrorNumber int = ERROR_NUMBER(),
            @ErrorSeverity int = ERROR_SEVERITY(),
            @ErrorState int = ERROR_STATE(),
            @ErrorProcedure nvarchar(128) = ERROR_PROCEDURE(),
            @ErrorLine int = ERROR_LINE(),
            @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();

    -- Log the error (in production, write to an error log table)
    -- INSERT INTO dbo.ErrorLog (...) VALUES (...)

    THROW;
END CATCH;