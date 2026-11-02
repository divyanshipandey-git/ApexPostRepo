# ADR 0018: Use stored procedures for stable reports and atomic stock adjustment

## Status

Accepted for the current SQL implementation.

## Context

Daily sales and low-stock reports need a stable SQL result contract; stock adjustment must validate, change inventory, and append movement history as one atomic operation. A generic assertion that procedures are faster is not a sufficient reason.

## Decision

- `usp_DailyBranchSales` performs the joined retail/service/tax aggregation with a half-open date range and optional branch filter.
- `usp_LowStockReport` applies an optional branch filter and caller-supplied validated threshold using separate all-branch and branch-filtered statements. It does not use `OPTION (RECOMPILE)`: the separate statements avoid the optional-branch predicate shape, and no controlled comparison demonstrates that recompilation offsets its per-execution compile cost. Threshold selectivity can still vary; reconsider recompilation only after representative SQL Server plan and compile-cost measurements show a repeatable benefit.
- `usp_AdjustStock` owns the lock, stock validation/update, movement-history insert, transaction, `XACT_ABORT`, `TRY...CATCH`, rollback, and `THROW` boundary.
- Shipping and auditor aggregate queries remain parameterized EF projections because those result shapes are straightforward and do not require a multi-statement atomic boundary.

## Alternatives considered

- Perform all three operations in LINQ: rejected for the stock operation's explicit locking/error boundary and the two stable report contracts.
- Put every report in a stored procedure: rejected because it adds deployment overhead without a specific consistency or query-contract need.
- Use dynamic SQL for optional filters: rejected because parameterized predicates are sufficient and avoid input concatenation.

## Consequences

Procedure changes are versioned in migrations and mirrored in standalone review files. The query plans need to be re-evaluated as data volume and selectivity change. Application calls remain parameterized; no procedure is a security boundary by itself.

## Implementation and verification

See `20261102000000_AddReportFilters.cs`, `SqlReportDataSource`, `SqlInventoryRepository`, and `database/stored-procedures/`. `SqlServerIntegrationTests` covers separated totals, thresholds, empty daily results, and stock rollback; these SQL tests were not run without the test connection.
