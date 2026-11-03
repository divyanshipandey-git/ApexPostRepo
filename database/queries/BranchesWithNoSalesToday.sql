-- Branches with no sales today (LEFT JOIN case)
-- Returns all active branches, showing zero sales where none occurred on the given date.
-- Uses a LEFT JOIN from Branches to the daily sales aggregation.

DECLARE @Date date = CONVERT(date, SYSUTCDATETIME());

SELECT b.BranchCode,
       b.Name AS BranchName,
       b.Region,
       COALESCE(sales.TransactionCount, 0) AS TransactionCount,
       COALESCE(sales.RetailSales, 0) AS RetailSales,
       COALESCE(sales.ServiceSales, 0) AS ServiceSales,
       COALESCE(sales.TotalSales, 0) AS TotalSales
FROM dbo.Branches AS b
LEFT JOIN (
    SELECT transactionRow.BranchCode,
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
    GROUP BY transactionRow.BranchCode
) AS sales ON sales.BranchCode = b.BranchCode
WHERE b.IsActive = 1
ORDER BY b.BranchCode;