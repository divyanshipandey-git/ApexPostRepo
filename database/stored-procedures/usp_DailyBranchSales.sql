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
END;
