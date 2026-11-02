SELECT DB_NAME() AS DatabaseName;

SELECT MigrationId
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;

SELECT ProcedureName = procedures.name
FROM sys.procedures AS procedures
WHERE procedures.name IN
(
    N'usp_DailyBranchSales',
    N'usp_LowStockReport',
    N'usp_AdjustStock'
)
ORDER BY procedures.name;

SELECT TableName = tables.name,
       RowCount = SUM(partitions.rows)
FROM sys.tables AS tables
INNER JOIN sys.partitions AS partitions ON partitions.object_id = tables.object_id
WHERE partitions.index_id IN (0, 1)
  AND tables.name IN
  (
      N'Branches', N'Products', N'BranchStock', N'CounterTransactions',
      N'Shipments', N'MoneyOrders', N'IdentityVerifications', N'StockMovements'
  )
GROUP BY tables.name
ORDER BY tables.name;
