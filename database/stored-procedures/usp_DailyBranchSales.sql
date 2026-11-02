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
END;
