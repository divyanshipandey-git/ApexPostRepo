-- Rank retail products by gross revenue within each branch region.
-- TransactionTaxRates is unique per transaction/category, preventing tax joins
-- from multiplying line revenue. The half-open timestamp range is SARGable.
DECLARE @StartUtc datetime2(7) = DATEADD(day, -30, SYSUTCDATETIME());
DECLARE @EndUtc datetime2(7) = SYSUTCDATETIME();
DECLARE @BranchCode varchar(10) = NULL;

WITH ProductRevenue AS
(
    SELECT branch.Region,
           product.Sku,
           product.Name AS ProductName,
           SUM(line.LineTotal + line.LineTotal * tax.Rate) AS Revenue
    FROM dbo.CounterTransactions AS transactionRow
    INNER JOIN dbo.Branches AS branch
        ON branch.BranchCode = transactionRow.BranchCode
    INNER JOIN dbo.TransactionLines AS line
        ON line.TransactionId = transactionRow.Id
       AND line.ItemType = 'Retail'
    INNER JOIN dbo.Products AS product
        ON product.Sku = line.ReferenceId
    INNER JOIN dbo.TransactionTaxRates AS tax
        ON tax.TransactionId = line.TransactionId
       AND tax.Category = line.TaxCategory
    WHERE transactionRow.Status = 'Paid'
      AND transactionRow.OccurredAtUtc >= @StartUtc
      AND transactionRow.OccurredAtUtc < @EndUtc
      AND (@BranchCode IS NULL OR transactionRow.BranchCode = @BranchCode)
    GROUP BY branch.Region, product.Sku, product.Name
),
RankedRevenue AS
(
    SELECT Region,
           Sku,
           ProductName,
           Revenue,
           ROW_NUMBER() OVER
               (PARTITION BY Region ORDER BY Revenue DESC, Sku ASC) AS RegionRank
    FROM ProductRevenue
)
SELECT Region, RegionRank, Sku, ProductName, Revenue
FROM RankedRevenue
WHERE RegionRank <= 10
ORDER BY Region, RegionRank;
