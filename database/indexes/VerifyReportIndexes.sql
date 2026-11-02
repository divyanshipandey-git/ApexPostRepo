SELECT tableName = OBJECT_SCHEMA_NAME(indexes.object_id) + '.' + OBJECT_NAME(indexes.object_id),
       indexes.name,
       indexes.type_desc,
       indexes.is_unique,
       indexes.has_filter,
       indexes.filter_definition
FROM sys.indexes AS indexes
WHERE indexes.object_id IN
(
    OBJECT_ID(N'dbo.BranchStock'),
    OBJECT_ID(N'dbo.CounterTransactions'),
    OBJECT_ID(N'dbo.Shipments'),
    OBJECT_ID(N'dbo.MoneyOrders')
)
  AND indexes.index_id > 0
ORDER BY tableName, indexes.name;
