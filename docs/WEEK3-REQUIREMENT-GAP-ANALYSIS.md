# Week 3 SQL remediation gap analysis

## Scope and method

This review covers the requested SQL-backed MVC reporting and persistence remediation. It uses the Week 3 assignment materials and the repository's Week 2 business requirements as the baseline. Couchbase, CounterTerminal, offline operation, SyncInbox/outbox, synchronization, and conflict resolution are expressly outside scope.

## Requirement status

| Area | Status | Evidence and notes |
|---|---|---|
| SQL-backed reports | Addressed | MVC report inputs cover date, branch, and validated low-stock threshold. Branch Managers are restricted to their home branch; Auditors and Administrators may filter by branch. See `ReportsController`, `ReportingService`, and the SQL-backed MVC tests. |
| Database-side filtering and aggregation | Addressed | Daily sales and low-stock reports use parameterized stored procedures. Shipping volume and money-order/identity-verification activity use date/branch predicates and SQL grouping rather than materializing all rows for application-side filtering. |
| Report DTO and persistence boundaries | Addressed | Infrastructure returns report DTOs; `IQueryable` remains inside Infrastructure. The MVC layer builds its view model from Application report results and branch options. |
| Stock procedure safety | Addressed | `usp_AdjustStock` validates required parameters and supported deltas, uses `XACT_ABORT`, locks the stock row, writes the movement in the same transaction, rolls back in `CATCH`, and rethrows. A SQL integration test verifies a failed adjustment leaves stock and movement history unchanged. |
| Set-based price revision | Addressed, semantics assumed | An Administrator-only workflow sets an absolute unit price for all products in a selected category. SQL persistence uses `ExecuteUpdateAsync` inside the unit-of-work transaction; the JSON adapter uses an atomic file mutation. The user did not provide a business-rule selection, so “set one fixed category price” remains an explicit assumption to confirm before production use. |
| Concurrency and exception handling | Addressed | SQL concurrency exceptions are translated and surfaced as HTTP 409; request-abort cancellation propagates; unexpected exceptions return safe Problem Details with trace/correlation identifiers. Stock adjustment and counter-sale persistence failure paths are logged or represented explicitly. See [the exception handling review](EXCEPTION-HANDLING-REVIEW.md). |
| SQL file organization and architecture decisions | Addressed | Deployment schema, migrations ownership, procedures, standalone report queries, index verification, seed guidance, and database verification are grouped under `database/`. ADR-0016 through ADR-0024 document the SQL and application boundaries. |
| Query translation evidence | Addressed | `PERFORMANCE.md` preserves historical diagnostics and includes three newly captured `ToQueryString()` outputs for current filtered EF report queries. |
| Actual-plan performance evidence | Partially addressed | Existing LocalDB actual-plan and `STATISTICS IO/TIME` measurements cover the documented synthetic workloads but predate the latest report-filter changes. The new filtered EF SQL was captured but not separately benchmarked. Re-run the workload against an approved representative dataset before drawing capacity or index conclusions. |
| SQL runtime and excluded features | Addressed | Web runtime uses SQL Server without JSON fallback. No Couchbase or offline synchronization implementation was introduced. |

## Validation state

The full Release solution test run passed with the SQL integration connection configured: 390 passed, 0 failed, and 0 skipped (112 Domain, 201 Application, 77 Web integration). The three focused SQL regression tests and the JSON-backed MVC subset also passed. Generated SQL translations were captured without executing against a database; the actual-plan limitation below remains.

## Remaining decisions and limitations

1. Confirm whether category price revision is intended to set an absolute price or apply another business rule. The current workflow sets an absolute price.
2. Run actual-plan and IO/time measurements for the current filtered report workloads on representative synthetic or approved anonymized data. Existing measurements are not presented as evidence for those changed query shapes.
3. Keep the retained LocalDB measurements scoped to their synthetic fixture; they are not production-capacity claims.
