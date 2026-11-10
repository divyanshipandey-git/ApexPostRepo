# Report index rationale

Index definitions are owned by EF migrations and entity configurations, not duplicated in this folder:

- `IX_BranchStock_Branch_Quantity_Sku` supports branch-scoped low-stock scans.
- `IX_CounterTransactions_Branch_Occurred_Status` supports branch/date sales reads; the documented test distribution did not show fewer reads than the baseline, so no gain is claimed.
- `IX_Shipments_Zone_Service_Created` supports zone/service/date workloads. Existing measurements did not show a read reduction on the retained synthetic slice.
- `IX_MoneyOrders_Branch_Created` covers branch/date auditor projections and reduced logical reads in the documented LocalDB fixture.

See [docs/WEEK3-COMPLETE-DOCUMENTATION.md#5-performance-evidence](../../docs/WEEK3-COMPLETE-DOCUMENTATION.md#5-performance-evidence) and the reproducible scripts under `database/performance/`. `VerifyReportIndexes.sql` only reads SQL Server catalog metadata. Do not add a competing `CREATE INDEX` definition here without a reviewed EF migration.
