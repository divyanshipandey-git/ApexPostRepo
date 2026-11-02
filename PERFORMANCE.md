# SQL performance evidence

## Method and isolated fixture

The corrected workload was run on 2026-11-03 against SQL Server LocalDB 17.0.4025.3 in the disposable catalog `ApexPost_PerfEvidence_Remediation`. It was created from all seven current EF migrations, populated by the repeatable synthetic setup, measured, and then dropped. It never targeted `ApexPostDb` or `App_Data`. The fixture contained 10 branches, 1,000 products, 10,000 stock rows, 50,000 paid transactions with lines/tax/payments, 20,000 shipments, 20,000 money orders, and 20,000 masked identity-verification records.

The repeatable setup and measurement scripts are [performance-evidence-setup.sql](database/performance/performance-evidence-setup.sql) and [performance-evidence-measurements.sql](database/performance/performance-evidence-measurements.sql). The old captured SQL Server output is [actual-plans-and-statistics.txt](docs/performance-plans/actual-plans-and-statistics.txt).

**The earlier forced-primary-key comparison is invalid for index-effect claims:** those “before” queries used index hints while secondary indexes remained present. The corrected run dropped only the supporting secondary index for the before query, executed the same unhinted SQL, recreated the index, and reran the query with normal optimizer choices. `STATISTICS IO`, `STATISTICS TIME`, and actual execution plans were enabled. No index hint was used to simulate the unindexed baseline.

## Corrected LocalDB results

One warm-cache run against the synthetic data produced these results. Reads and times are one-run observations; very small CPU/elapsed values are subject to LocalDB timing resolution.

| Workload | Index state | Logical reads (primary table) | Actual plan operator | Estimated rows → actual rows | CPU / elapsed |
|---|---|---:|---|---:|---:|
| Low stock, branch P01 | absent / present | 12 / 12 (plus 19 product reads in each run) | clustered PK scan in both | 447.7 → 197 | 1 ms / 1 ms in both |
| Daily paid sales, branch P01, last 7 days | absent / present | 950 / 950 | clustered PK scan in both | 2,026.8 → 1,008 | 5 ms / 5 ms in both |
| Shipment zone/service/date, last 7 days | absent / present | 320 / 320 | clustered PK scan in both | 706.7 → 336 | 3 ms / 3 ms in both |
| Money orders, branch P01, last 7 days | absent / present | 389 / 10 | clustered PK scan → `IX_MoneyOrders_Branch_Created` seek | 810.7 → 1,008 | 3/7 ms → 0/3 ms |

The money-order plan reduced the measured base-table logical reads by about 97% in this run. The optimizer did not choose the added low-stock, daily-sales, or shipment index for these fixture distributions, so no read benefit was observed for those query shapes. The estimates also differ materially from actual rows, especially daily sales and low stock; the synthetic distribution is not a cardinality model for production.

The script additionally compared one rollback-only stock update and insert:

| Write operation | Supporting stock index present | Supporting stock index absent |
|---|---:|---:|
| Update `P01` / `PERF-0001` | 12 BranchStock logical reads | 2 BranchStock logical reads |
| Insert one BranchStock row | 17 BranchStock logical reads | 4 BranchStock logical reads |

The insert also performed two logical reads each against the `Products` and `Branches` foreign-key tables. Both write cases were rolled back; their observed elapsed time was below useful single-millisecond resolution. These are operation-level reads, not throughput or concurrency measurements.

Selected index storage from `sys.dm_db_partition_stats` (10,000 stock rows; 50,000 transactions; 20,000 shipments and money orders):

| Index | Reserved KB | Used KB |
|---|---:|---:|
| `IX_BranchStock_Branch_Quantity_Sku` | 392 | 296 |
| `IX_CounterTransactions_Branch_Occurred_Status` | 2,184 | 2,104 |
| `IX_Shipments_Zone_Service_Created` | 1,096 | 992 |
| `IX_MoneyOrders_Branch_Created` | 1,160 | 1,080 |

The non-SARGable `YEAR(OccurredAtUtc)` and equivalent half-open year range each read 29 transaction pages and returned 5,000 P01 rows in this fixture. The plans could seek on branch, but the tested year covered every generated row; this run showed no measurable date-predicate gain.

These are actual LocalDB execution-plan observations from this synthetic fixture, not production capacity evidence. The test catalog was removed after capture. The SQL-backed test `TransactionRepository_LoadsCollectionsWithBoundedSplitQueryCommands` also ran with the isolated SQL suite and passed its assertion of three reader commands for four transactions.

## Historical EF SQL samples (2026-11-01)

These translations were captured before the current date/branch-filtered report paths were added. They are retained as historical diagnostics, not descriptions of the current MVC report queries. Stored-procedure-backed daily-sales and low-stock reports are invoked as parameterized commands and do not have `ToQueryString()` output.

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

## Current filtered EF query samples (2026-11-02)

The following three translations were captured with EF Core `ToQueryString()` using the current `SqlQueryDiagnostics.Capture` implementation. They represent the current date-and-branch-filtered shipment and branch service activity query shapes. The sample uses UTC date `2026-11-02` and branch `P01`; capturing SQL does not execute the queries or measure their runtime.

### Filtered shipment volume

```sql
DECLARE @p datetime2 = '2026-11-02T00:00:00.0000000Z';
DECLARE @p1 datetime2 = '2026-11-03T00:00:00.0000000Z';
DECLARE @branch varchar(10) = 'P01';

SELECT [s].[ZoneCode] AS [DestinationZone], [s].[ServiceLevel], COUNT(*) AS [ShipmentCount]
FROM [Shipments] AS [s]
WHERE [s].[CreatedAtUtc] >= @p AND [s].[CreatedAtUtc] < @p1 AND [s].[BranchCode] = @branch
GROUP BY [s].[ZoneCode], [s].[ServiceLevel]
```

### Money-order activity by branch

```sql
DECLARE @p datetime2 = '2026-11-02T00:00:00.0000000Z';
DECLARE @p1 datetime2 = '2026-11-03T00:00:00.0000000Z';
DECLARE @branch varchar(10) = 'P01';

SELECT [m].[BranchCode], COUNT(*) AS [Count]
FROM [MoneyOrders] AS [m]
WHERE [m].[CreatedAtUtc] >= @p AND [m].[CreatedAtUtc] < @p1 AND [m].[BranchCode] IS NOT NULL AND [m].[BranchCode] = @branch
GROUP BY [m].[BranchCode]
```

### Identity-verification activity by branch

```sql
DECLARE @p datetime2 = '2026-11-02T00:00:00.0000000Z';
DECLARE @p1 datetime2 = '2026-11-03T00:00:00.0000000Z';
DECLARE @branch varchar(10) = 'P01';

SELECT [m].[BranchCode], COUNT(*) AS [Count]
FROM [MoneyOrders] AS [m]
INNER JOIN [IdentityVerifications] AS [i] ON [m].[Id] = [i].[MoneyOrderId]
WHERE [m].[BranchCode] IS NOT NULL AND [i].[VerifiedAtUtc] >= @p AND [i].[VerifiedAtUtc] < @p1 AND [m].[BranchCode] = @branch
GROUP BY [m].[BranchCode]
```

## Non-SARGable comparison

The corrected run compares `YEAR(OccurredAtUtc) = YEAR(SYSUTCDATETIME())` with the equivalent calendar-year half-open range for branch `P01`. Both read 29 logical pages and completed within 1 ms; both returned 5,000 rows because the fixture's date range covered all generated transactions. The SARGable version remains the implementation standard because it permits timestamp-range seeking when data spans years; this run does not demonstrate a numeric gain and does not claim one.

## Index choices and trade-offs

| Index | Read rationale | Write/storage cost |
|---|---|---|
| `(BranchCode, Quantity, Sku)` on `BranchStock` | Branch equality then low-quantity range; SKU supports the join/output. | Every stock adjustment maintains the extra key. Product name is intentionally not included because it belongs to `Products` and would widen every stock row. |
| `(BranchCode, OccurredAtUtc, Status)` on `CounterTransactions` | Branch/date reports have a stable leading branch and range. | Each sale writes the index. The status-after-range selectivity needs a valid before/after measurement. |
| `(ZoneCode, ServiceLevel, CreatedAtUtc)` on `Shipments` | Matches zone/service equality followed by date range. | Each shipment insert updates the index; including extra receipt columns would increase index size. |
| `(BranchCode, CreatedAtUtc, Id)` including amount/fee/transaction on `MoneyOrders` | Intended to support auditor branch/date ordering and cover displayed monetary fields. | Included columns increase insert and storage cost; index benefit and size have not been validly measured here. |

These are schema design rationales, not measured performance claims. No new index DDL is proposed from the invalid historical comparison.

## Remaining limitation

The corrected SQL workload and command-count test were executed against isolated catalogs, and their synthetic results are summarized above. LocalDB observations do not substitute for production concurrency, hardware, cache pressure, or data distribution; rerun against an approved anonymized production-like volume before capacity or index decisions.

## Normalization anomalies prevented

- **Insert anomaly:** a product must exist once in `Products` before branch inventory can reference its SKU in `BranchStock`; product details are not repeated in each branch's stock row.
- **Update anomaly:** a branch name or product name is maintained in one `Branches` or `Products` row rather than copied into every transaction line or stock record.
- **Delete anomaly:** `NO ACTION` relationships from transactions, products, staff, and branches to financial/history rows prevent deleting a referenced entity from silently erasing its sales, payment, stock movement, or audit history.

## Split-query and N+1 behavior

`SqlCounterTransactionRepository.Full` loads transaction `Lines` and `TaxRates` collections plus the one-to-one `Payment`. Joining both collections in one SQL statement can multiply line rows by tax-rate rows. The repository uses `AsSplitQuery()` to issue one root query and one query per collection while avoiding that cartesian multiplication; the trade-off is additional round trips and a possibility of seeing data across statements unless the caller uses a transaction.

The SQL integration test `TransactionRepository_LoadsCollectionsWithBoundedSplitQueryCommands` seeds four transactions and asserts a fixed three reader commands (root, lines, tax rates), rather than one query per transaction. This is a repeatable proof specification, not a measured result until the LocalDB test is run.
