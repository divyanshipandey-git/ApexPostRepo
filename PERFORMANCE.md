# SQL performance evidence

## Method and retained fixture

Measurements were taken on 2026-11-01 using SQL Server LocalDB `(localdb)\MSSQLLocalDB`, catalog `ApexPost_PerfEvidence_20261101b`. It is a retained, isolated fictional-data catalog, never `ApexPostDb` or `App_Data`. The fixture contains 10 branches, 1,000 products, 10,000 stock rows, 50,000 paid transactions with lines/tax/payments, 20,000 shipments, and 20,000 money orders.

The reproducible setup and workload scripts are [performance-evidence-setup.sql](docs/sql/performance-evidence-setup.sql) and [performance-evidence-measurements.sql](docs/sql/performance-evidence-measurements.sql). The saved SQL Server actual-plan XML and `SET STATISTICS IO, TIME ON` output is [actual-plans-and-statistics.txt](docs/performance-plans/actual-plans-and-statistics.txt). Results are warm-cache LocalDB observations, not production capacity estimates.

The baseline forces the relevant primary-key access path with an `INDEX` hint only in this isolated evidence script. The after measurement lets the optimizer choose the deployed migration index. This compares real access paths without changing production schema.

## Actual-plan and IO/time results

| Workload | Deployed index | Baseline reads / CPU / elapsed | Optimizer reads / CPU / elapsed | Actual finding |
|---|---|---:|---:|---|
| Low stock for `P01`, threshold 5 | `IX_BranchStock_Branch_Quantity_Sku` | 14 `BranchStock` + 21 `Products` / 0 ms / 2 ms | 14 + 21 / 0 ms / 1 ms | The optimizer retained the same compact branch range on this 1,000-row branch slice. No measured read reduction. |
| Daily paid sales for `P01`, 7 days | `IX_CounterTransactions_Branch_Occurred_Status` | 1,029 / 15 ms / 11 ms | 1,029 / 16 ms / 6 ms | The current key order did not reduce reads for this status predicate after the date range. It remains an evidence-backed limitation. |
| Shipments for `ZONE1`/`Standard`, 7 days | `IX_Shipments_Zone_Service_Created` | 355 / 0 ms / 3 ms | 355 / 16 ms / 4 ms | The returned fixture slice did not yield a lower-read plan. No improvement is claimed. |
| Auditor money orders for `P01`, 7 days | `IX_MoneyOrders_Branch_Created` with amount/fee/transaction includes | 426 + sort worktable / 0 ms / 16 ms | 11 / 0 ms / 6 ms | The index removed the broad primary-key scan and sort work, reducing `MoneyOrders` logical reads by 415 (97%). |

The source output contains actual plan XML following each labelled query, plus the exact SQL Server messages. Physical reads vary with cache state and are therefore not used to claim an improvement.

## Generated SQL samples

These are the SQL Server translations captured with EF Core `ToQueryString()` by `SqlQueryDiagnostics.Capture` on 2026-11-01. The translation output is preserved here as emitted; parameterized runtime operations and stored-procedure calls remain separate from these read-query examples.

### Paid branch sales

```sql
SELECT [c].[BranchCode] AS [Branch], COUNT_BIG(*) AS [Count], COALESCE(SUM([c].[Total]), 0.0) AS [Gross]
FROM [CounterTransactions] AS [c]
WHERE [c].[Status] = 'Paid' AND [c].[OccurredAtUtc] >= DATEADD(day, CAST(-30.0E0 AS int), CAST(SYSUTCDATETIME() AS datetimeoffset))
GROUP BY [c].[BranchCode]
```

### Low stock with product name

```sql
SELECT [b].[BranchCode], [b].[Sku], [p].[Name], [b].[Quantity]
FROM [BranchStock] AS [b]
INNER JOIN [Products] AS [p] ON [b].[Sku] = [p].[Sku]
WHERE [b].[Quantity] <= 5
```

### Shipment volume by zone and service

```sql
SELECT [s].[ZoneCode] AS [DestinationZone], [s].[ServiceLevel], COUNT_BIG(*) AS [Count]
FROM [Shipments] AS [s]
WHERE [s].[CreatedAtUtc] >= DATEADD(day, CAST(-30.0E0 AS int), CAST(SYSUTCDATETIME() AS datetimeoffset))
GROUP BY [s].[ZoneCode], [s].[ServiceLevel]
```

### Auditor money-order search

```sql
SELECT [m].[Id], [m].[BranchCode], [m].[Amount], [m].[Fee], [m].[TransactionId], [m].[CreatedAtUtc]
FROM [MoneyOrders] AS [m]
WHERE [m].[CreatedAtUtc] >= DATEADD(day, CAST(-30.0E0 AS int), CAST(SYSUTCDATETIME() AS datetimeoffset))
ORDER BY [m].[CreatedAtUtc] DESC
```

## Non-SARGable comparison

The saved run compares `YEAR(OccurredAtUtc) = YEAR(SYSUTCDATETIME())` with the equivalent calendar-year half-open range for branch `P01`. Both read 38 logical pages and completed in 1 ms on this fixture because all matching synthetic rows fall in the same small current-year branch range. The SARGable version remains the implementation standard because it permits timestamp-range seeking when data spans years; this run does not demonstrate a numeric gain and does not claim one.

## Index choices and trade-offs

| Index | Read rationale | Write/storage cost |
|---|---|---|
| `(BranchCode, Quantity, Sku)` on `BranchStock` | Branch equality then low-quantity range; SKU supports the join/output. | Every stock adjustment maintains the extra key. Product name is intentionally not included because it belongs to `Products` and would widen every stock row. |
| `(BranchCode, OccurredAtUtc, Status)` on `CounterTransactions` | Branch/date reports have a stable leading branch and range. | Each sale writes the index. The measured status-after-range limitation should be reconsidered only with representative production selectivity. |
| `(ZoneCode, ServiceLevel, CreatedAtUtc)` on `Shipments` | Matches zone/service equality followed by date range. | Each shipment insert updates the index; including extra receipt columns would increase index size. |
| `(BranchCode, CreatedAtUtc, Id)` including amount/fee/transaction on `MoneyOrders` | Supports auditor branch/date ordering and covers the displayed monetary fields. | Included columns increase insert and storage cost, justified here by the measured 426-to-11 logical-read reduction. |

No index DDL was added from these measurements. Three workloads showed no lower-read plan on this fixture, so changing their index design without broader representative evidence would be premature.

## Remaining limitation

LocalDB actual-plan XML and IO/time evidence is complete for the required synthetic workloads. The remaining limitation is representativeness: these measurements do not include production concurrency, hardware, cache pressure, or production data distribution. Re-run the documented scripts against an approved anonymized production-like volume before making capacity or index-change decisions.
