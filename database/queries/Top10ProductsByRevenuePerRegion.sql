-- Top 10 products by revenue per region
-- Uses a CTE with ROW_NUMBER() to rank products within each region by total revenue.
-- Filters to paid transactions within the specified date range.

DECLARE @StartUtc datetime2(7) = DATEADD(day, -30, SYSUTCDATETIME());
DECLARE @EndUtc datetime2(7) = SYSUTCDATETIME();

WITH ProductRevenue AS (
    SELECT b.Region,
           line.ReferenceId AS Sku,
           p.Name AS ProductName,
           SUM(line.LineTotal + line.LineTotal * tax.Rate) AS TotalRevenue
    FROM dbo.CounterTransactions AS tx
    INNER JOIN dbo.TransactionLines AS line
        ON line.TransactionId = tx.Id
    INNER JOIN dbo.TransactionTaxRates AS tax
        ON tax.TransactionId = line.TransactionId
       AND tax.Category = line.TaxCategory
    INNER JOIN dbo.Branches AS b
        ON b.BranchCode = tx.BranchCode
    INNER JOIN dbo.Products AS p
        ON p.Sku = line.ReferenceId
    WHERE tx.Status = 'Paid'
      AND tx.OccurredAtUtc >= @StartUtc
      AND tx.OccurredAtUtc < @EndUtc
      AND line.ItemType = 'Retail'
      AND line.ReferenceId IS NOT NULL
    GROUP BY b.Region, line.ReferenceId, p.Name
),
RankedProducts AS (
    SELECT Region,
           Sku,
           ProductName,
           TotalRevenue,
           ROW_NUMBER() OVER (PARTITION BY Region ORDER BY TotalRevenue DESC) AS RankInRegion
    FROM ProductRevenue
)
SELECT Region,
       Sku,
       ProductName,
       TotalRevenue
FROM RankedProducts
WHERE RankInRegion <= 10
ORDER BY Region, RankInRegion;