/* Run only against the isolated synthetic performance catalog. */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
SET STATISTICS XML ON;

PRINT 'LOW-STOCK BASELINE (forced clustered key)';
SELECT stock.BranchCode, stock.Sku, product.Name, stock.Quantity
FROM dbo.BranchStock AS stock WITH (INDEX(PK_BranchStock))
JOIN dbo.Products AS product ON product.Sku = stock.Sku
WHERE stock.BranchCode = 'P01' AND stock.Quantity <= 5;
PRINT 'LOW-STOCK INDEXED';
SELECT stock.BranchCode, stock.Sku, product.Name, stock.Quantity
FROM dbo.BranchStock AS stock
JOIN dbo.Products AS product ON product.Sku = stock.Sku
WHERE stock.BranchCode = 'P01' AND stock.Quantity <= 5;

PRINT 'DAILY-SALES BASELINE (forced primary key)';
SELECT COUNT_BIG(*) AS TransactionCount, SUM(transactionRow.Total) AS TotalSales
FROM dbo.CounterTransactions AS transactionRow WITH (INDEX(PK_CounterTransactions))
WHERE transactionRow.BranchCode = 'P01' AND transactionRow.Status = 'Paid'
  AND transactionRow.OccurredAtUtc >= DATEADD(day, -7, SYSUTCDATETIME())
  AND transactionRow.OccurredAtUtc < SYSUTCDATETIME();
PRINT 'DAILY-SALES INDEXED';
SELECT COUNT_BIG(*) AS TransactionCount, SUM(transactionRow.Total) AS TotalSales
FROM dbo.CounterTransactions AS transactionRow
WHERE transactionRow.BranchCode = 'P01' AND transactionRow.Status = 'Paid'
  AND transactionRow.OccurredAtUtc >= DATEADD(day, -7, SYSUTCDATETIME())
  AND transactionRow.OccurredAtUtc < SYSUTCDATETIME();

PRINT 'SHIPMENT BASELINE (forced primary key)';
SELECT Id, BranchCode, ZoneCode, ServiceLevel, CreatedAtUtc
FROM dbo.Shipments WITH (INDEX(PK_Shipments))
WHERE ZoneCode = 'ZONE1' AND ServiceLevel = 'Standard'
  AND CreatedAtUtc >= DATEADD(day, -7, SYSUTCDATETIME());
PRINT 'SHIPMENT INDEXED';
SELECT Id, BranchCode, ZoneCode, ServiceLevel, CreatedAtUtc
FROM dbo.Shipments
WHERE ZoneCode = 'ZONE1' AND ServiceLevel = 'Standard'
  AND CreatedAtUtc >= DATEADD(day, -7, SYSUTCDATETIME());

PRINT 'MONEY-ORDER BASELINE (forced primary key)';
SELECT Id, BranchCode, Amount, Fee, TransactionId, CreatedAtUtc
FROM dbo.MoneyOrders WITH (INDEX(PK_MoneyOrders))
WHERE BranchCode = 'P01' AND CreatedAtUtc >= DATEADD(day, -7, SYSUTCDATETIME())
ORDER BY CreatedAtUtc DESC;
PRINT 'MONEY-ORDER INDEXED';
SELECT Id, BranchCode, Amount, Fee, TransactionId, CreatedAtUtc
FROM dbo.MoneyOrders
WHERE BranchCode = 'P01' AND CreatedAtUtc >= DATEADD(day, -7, SYSUTCDATETIME())
ORDER BY CreatedAtUtc DESC;

PRINT 'NON-SARGABLE DATE PREDICATE';
SELECT COUNT_BIG(*) AS TransactionCount
FROM dbo.CounterTransactions
WHERE BranchCode = 'P01' AND YEAR(OccurredAtUtc) = YEAR(SYSUTCDATETIME());
PRINT 'SARGABLE DATE-RANGE PREDICATE';
SELECT COUNT_BIG(*) AS TransactionCount
FROM dbo.CounterTransactions
WHERE BranchCode = 'P01'
  AND OccurredAtUtc >= DATEFROMPARTS(YEAR(SYSUTCDATETIME()), 1, 1)
  AND OccurredAtUtc < DATEFROMPARTS(YEAR(SYSUTCDATETIME()) + 1, 1, 1);
