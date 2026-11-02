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
END;
