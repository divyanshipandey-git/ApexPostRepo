/* Run only against the isolated synthetic performance catalog. */
IF DB_NAME() NOT LIKE N'ApexPost_PerfEvidence[_]%'
    THROW 51200, 'Run performance measurements only in an ApexPost_PerfEvidence_* catalog.', 1;
GO

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

DECLARE @StartUtc datetime2(7) = DATEADD(day, -7, SYSUTCDATETIME());
DECLARE @EndUtc datetime2(7) = SYSUTCDATETIME();

DROP INDEX IF EXISTS IX_BranchStock_Branch_Quantity_Sku ON dbo.BranchStock;
PRINT 'LOW-STOCK BEFORE (supporting secondary index absent; optimizer chooses the plan)';
SELECT stock.BranchCode, stock.Sku, product.Name, stock.Quantity
FROM dbo.BranchStock AS stock
JOIN dbo.Products AS product ON product.Sku = stock.Sku
WHERE stock.BranchCode = 'P01' AND stock.Quantity <= 5;
CREATE INDEX IX_BranchStock_Branch_Quantity_Sku
    ON dbo.BranchStock (BranchCode, Quantity, Sku);
PRINT 'LOW-STOCK AFTER (supporting secondary index present)';
SELECT stock.BranchCode, stock.Sku, product.Name, stock.Quantity
FROM dbo.BranchStock AS stock
JOIN dbo.Products AS product ON product.Sku = stock.Sku
WHERE stock.BranchCode = 'P01' AND stock.Quantity <= 5;

DROP INDEX IF EXISTS IX_CounterTransactions_Branch_Occurred_Status ON dbo.CounterTransactions;
PRINT 'DAILY-SALES BEFORE (supporting secondary index absent; optimizer chooses the plan)';
SELECT COUNT_BIG(*) AS TransactionCount, SUM(transactionRow.Total) AS TotalSales
FROM dbo.CounterTransactions AS transactionRow
WHERE transactionRow.BranchCode = 'P01' AND transactionRow.Status = 'Paid'
  AND transactionRow.OccurredAtUtc >= @StartUtc
  AND transactionRow.OccurredAtUtc < @EndUtc;
CREATE INDEX IX_CounterTransactions_Branch_Occurred_Status
    ON dbo.CounterTransactions (BranchCode, OccurredAtUtc, Status);
PRINT 'DAILY-SALES AFTER (supporting secondary index present)';
SELECT COUNT_BIG(*) AS TransactionCount, SUM(transactionRow.Total) AS TotalSales
FROM dbo.CounterTransactions AS transactionRow
WHERE transactionRow.BranchCode = 'P01' AND transactionRow.Status = 'Paid'
  AND transactionRow.OccurredAtUtc >= @StartUtc
  AND transactionRow.OccurredAtUtc < @EndUtc;

DROP INDEX IF EXISTS IX_Shipments_Zone_Service_Created ON dbo.Shipments;
PRINT 'SHIPMENT BEFORE (supporting secondary index absent; optimizer chooses the plan)';
SELECT Id, BranchCode, ZoneCode, ServiceLevel, CreatedAtUtc
FROM dbo.Shipments
WHERE ZoneCode = 'ZONE1' AND ServiceLevel = 'Standard'
  AND CreatedAtUtc >= @StartUtc AND CreatedAtUtc < @EndUtc;
CREATE INDEX IX_Shipments_Zone_Service_Created
    ON dbo.Shipments (ZoneCode, ServiceLevel, CreatedAtUtc);
PRINT 'SHIPMENT AFTER (supporting secondary index present)';
SELECT Id, BranchCode, ZoneCode, ServiceLevel, CreatedAtUtc
FROM dbo.Shipments
WHERE ZoneCode = 'ZONE1' AND ServiceLevel = 'Standard'
  AND CreatedAtUtc >= @StartUtc AND CreatedAtUtc < @EndUtc;

DROP INDEX IF EXISTS IX_MoneyOrders_Branch_Created ON dbo.MoneyOrders;
PRINT 'MONEY-ORDER BEFORE (supporting secondary index absent; optimizer chooses the plan)';
SELECT Id, BranchCode, Amount, Fee, TransactionId, CreatedAtUtc
FROM dbo.MoneyOrders
WHERE BranchCode = 'P01' AND CreatedAtUtc >= @StartUtc AND CreatedAtUtc < @EndUtc
ORDER BY CreatedAtUtc DESC;
CREATE INDEX IX_MoneyOrders_Branch_Created
    ON dbo.MoneyOrders (BranchCode, CreatedAtUtc, Id)
    INCLUDE (Amount, Fee, TransactionId);
PRINT 'MONEY-ORDER AFTER (supporting secondary index present)';
SELECT Id, BranchCode, Amount, Fee, TransactionId, CreatedAtUtc
FROM dbo.MoneyOrders
WHERE BranchCode = 'P01' AND CreatedAtUtc >= @StartUtc AND CreatedAtUtc < @EndUtc
ORDER BY CreatedAtUtc DESC;

PRINT 'INDEXED STOCK UPDATE WRITE OVERHEAD (transaction rolled back)';
BEGIN TRANSACTION;
UPDATE dbo.BranchStock SET Quantity = Quantity + 1
WHERE BranchCode = 'P01' AND Sku = 'PERF-0001';
ROLLBACK TRANSACTION;

PRINT 'INDEXED STOCK INSERT WRITE OVERHEAD (transaction rolled back)';
BEGIN TRANSACTION;
INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedBy)
VALUES ('P01', 'PERF-INSERT-ONLY', 10, 'performance-fixture');
ROLLBACK TRANSACTION;

DROP INDEX IF EXISTS IX_BranchStock_Branch_Quantity_Sku ON dbo.BranchStock;
PRINT 'UNINDEXED STOCK UPDATE WRITE OVERHEAD (transaction rolled back)';
BEGIN TRANSACTION;
UPDATE dbo.BranchStock SET Quantity = Quantity + 1
WHERE BranchCode = 'P01' AND Sku = 'PERF-0001';
ROLLBACK TRANSACTION;

PRINT 'UNINDEXED STOCK INSERT WRITE OVERHEAD (transaction rolled back)';
BEGIN TRANSACTION;
INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedBy)
VALUES ('P01', 'PERF-INSERT-ONLY', 10, 'performance-fixture');
ROLLBACK TRANSACTION;

CREATE INDEX IX_BranchStock_Branch_Quantity_Sku
    ON dbo.BranchStock (BranchCode, Quantity, Sku);

PRINT 'INDEX STORAGE (8 KB pages, grouped by object and index)';
SELECT OBJECT_NAME(partitionStats.object_id) AS TableName,
       indexInfo.name AS IndexName,
       SUM(partitionStats.row_count) AS TotalRows,
       SUM(partitionStats.reserved_page_count) * 8 AS ReservedKB,
       SUM(partitionStats.used_page_count) * 8 AS UsedKB
FROM sys.dm_db_partition_stats AS partitionStats
INNER JOIN sys.indexes AS indexInfo
    ON indexInfo.object_id = partitionStats.object_id
   AND indexInfo.index_id = partitionStats.index_id
WHERE OBJECT_NAME(partitionStats.object_id) IN
    ('BranchStock', 'CounterTransactions', 'Shipments', 'MoneyOrders')
GROUP BY partitionStats.object_id, indexInfo.name
ORDER BY TableName, IndexName;

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
