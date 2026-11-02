DECLARE @StartUtc datetime2(7) = DATEADD(day, -30, SYSUTCDATETIME());
DECLARE @EndUtc datetime2(7) = SYSUTCDATETIME();
DECLARE @BranchCode varchar(10) = NULL;

SELECT orders.BranchCode,
       COUNT_BIG(*) AS MoneyOrderCount,
       SUM(orders.Amount) AS AmountTotal,
       SUM(orders.Fee) AS FeeTotal
FROM dbo.MoneyOrders AS orders
WHERE orders.CreatedAtUtc >= @StartUtc
  AND orders.CreatedAtUtc < @EndUtc
  AND (@BranchCode IS NULL OR orders.BranchCode = @BranchCode)
GROUP BY orders.BranchCode
ORDER BY orders.BranchCode;
