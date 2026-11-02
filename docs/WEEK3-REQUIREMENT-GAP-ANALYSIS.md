# Week 3 requirement gap analysis

## Scope and evidence

This audit covers the Week 3 SQL remediation while preserving Week 2 behavior. It excludes Couchbase, CounterTerminal, offline operation, synchronization, SyncInbox/outbox, replication, and conflict resolution. SQL-backed tests were run with `APEXPOST_SQL_TEST_CONNECTION` targeting an isolated `ApexPost_SqlIT_*` catalog. The test fixtures applied migrations and removed their temporary catalogs.

## Requirement-by-requirement status

| Requirement | Implementation and evidence | Status | Remaining caveat |
|---|---|---|---|
| Daily sales split by retail/services, date and branch filters | `usp_DailyBranchSales`, `SqlReportDataSource`, `/Reports`; SQL integration covers populated and zero-sales branch results and invalid branch handling | Verified | Stored procedure scripts are deployed by migrations; the standalone SQLCMD wrappers have not been independently exercised. |
| Branches with no sales today | `sql/queries/branches-with-no-sales-today.sql` uses paid status, `NOT EXISTS`, and a half-open UTC range; reporting query scripts executed against the synthetic LocalDB fixture | Verified | Synthetic fixture only; the query is a read-only script, not a separate MVC page. |
| Top 10 products by revenue per region | SQL query and `/Reports/TopProducts`; SQL query uses SQL-side aggregation, tax, filtering, and per-region ranking. Application and Web tests verify SKU result and route. | Verified | Results are restricted to paid retail lines with a product reference and current catalog match. |
| Shipping volume, money orders, low stock, identity verifications | Canonical query/procedure definitions; SQL-side report filters; paginated Auditor/Administrator money-order and identity-verification screens with masked IDs | Verified | Detail report routes are intentionally not available to Branch Managers. |
| Root SQL folder without duplicate procedure sources | SQLCMD wrappers and standalone demos under `sql/`; canonical definitions remain under `database/` and EF migrations | Implemented | SQLCMD wrapper execution is not included in the automated test suite. |
| Counter-sale and stock-adjustment transaction demonstrations | Rollback-by-default scripts under `sql/transactions`; guarded database-name checks | Implemented | Do not run demos against persistent or production databases. |
| Two-session blocking and deadlock guidance | Two-session scripts and deadlock-avoidance guide under `sql/concurrency/` | Implemented | No environment-specific blocking/deadlock exercise was run. |
| `usp_AdjustStock` validation and atomic stock/history update | Hardened procedure and migration; success, invalid adjustment and rollback behavior covered by SQL tests | Verified | Tests establish behavior under LocalDB, not production contention. |
| `usp_LowStockReport` branch filter and query-plan choice | Separate all-branch and branch-filtered query paths; SQL-backed tests and synthetic plan/IO run | Verified | Synthetic workload cardinality is not representative of production. |
| `usp_DailyBranchSales` no-sales and input handling | Validates date and branch, uses half-open range, counts distinct transactions and returns zero-valued branch rows | Verified | None identified in the tested scenarios. |
| Least privilege | `sql/README.md` documents runtime role, procedure execution and separate migration identity | Partially implemented | A live restricted SQL principal was not provisioned and permission-tested; deployment grants must match the configured runtime operations. |
| Money-order sale atomicity | SQL counter-sale stages stock, transaction, shipment, money order and identity verification within the required Unit of Work transaction; SQL success/failure and fresh-context reads pass | Verified for SQL runtime | Legacy JSON adapters remain compatibility/import/test paths and do not provide SQL transaction guarantees. |
| Unit of Work boundaries | SQL Unit of Work coordinates the provider execution strategy, transaction, single save and concurrency translation; stored stock adjustment is transaction-owning | Partial | Several unrelated service/write workflows retain optional Unit of Work dependencies. They were not globally refactored as part of the SQL sale fix. |
| Commit-count tests | Spy tests cover counter-sales and selected standalone writes; SQL tests verify persisted state and rollback | Partial | A single shared commit-count assertion does not cover every standalone write, refund, void, and administration path. |
| Set-based price revision | SQL repository uses `ExecuteUpdateAsync`; SQL-backed category-price test ran in the enabled suite | Verified | Fixed absolute category price remains the documented business-rule assumption. |
| Owned types | Current persisted schema has no applicable owned value object; no artificial EF mapping was introduced | Not applicable | Revisit if a persisted address/value object is added. |
| Split query and N+1 evidence | `AsSplitQuery`; SQL integration test asserts a bounded three-reader-command load for four transactions | Verified | The measured command count is specific to the tested repository graph. |
| SQL-side filtering and projection | Summary reports use stored procedures or SQL filters/aggregates; auditor detail pages perform count, filtering, ordering, and pagination in SQL; top products aggregate/rank in SQL | Partial | Legacy broad projection methods remain available for existing consumers and tests. The new user-facing auditor pages do not load full tables to paginate. |
| MVC report workflows | `/Reports` summary, money-order and identity-verification pagination, and top-products-by-region workflow; role, branch and filter validation are covered by SQL-backed HTTP tests | Verified | Live production identity/role configuration is outside these integration tests. |
| Exception mapping | Unit of Work maps concurrency and unique-key errors; middleware maps validation/not-found/conflict and unexpected errors to safe responses; middleware tests pass | Partial | Duplicate-key and rowversion-conflict outcomes are not exercised through a SQL-backed HTTP request in this suite. |
| Over-posting | SQL-backed MVC test submits attacker-controlled IDs, status, branch, timestamps, audit fields and amounts, then re-reads persisted transaction/order state | Verified | Coverage is focused on the counter-sale and money-order form. |
| Index/performance evidence | `PERFORMANCE.md` records corrected synthetic LocalDB actual-plan, logical-read, elapsed/CPU, estimated/actual-row, write-read and index-storage comparisons | Verified as synthetic evidence | Single-run LocalDB figures are not production capacity or concurrency claims. |
| ADR coverage and numbering | ADR-0016 through ADR-0025 cover Week 3 schema, query, persistence, transaction, audit, DTO, least-privilege and PKCS dependency decisions | Implemented | The pre-existing ADR-0002 deletion is preserved; its PKCS decision was moved to ADR-0025 to avoid historical numbering collision. |
| Documentation | README, test guide, rubric, performance notes, SQL guide and final trainer verification document describe the completed work and verification | Implemented | Historical Week 3 documents retain context; this gap analysis distinguishes verified work from remaining deployment caveats. |
| SQL integration suite and database safety | Full SQL-enabled Release suite applies migrations in isolated generated test databases; fixtures clean up automatically. LocalDB inventory shows only persistent `ApexPostDb`, no `ApexPost_SqlIT_*` or performance evidence catalogs. | Verified | `ApexPostDb` was not used for tests or performance fixture writes. |
| Week 2 behavior and exclusions | Domain, Application, and Web suites pass; no excluded offline/sync/Couchbase/CounterTerminal features added | Verified | See existing assignment traceability for individual Week 2 requirements. |

## Test evidence for this checkout

The explicit Release build succeeded with 0 warnings and 0 errors. The full SQL-enabled Release test run passed **407 tests**: 112 Domain, 214 Application, and 81 Web integration tests; 0 failed and 0 skipped. The SQL-backed suite applied the current migrations to isolated `ApexPost_SqlIT_*` catalogs and exercised persistence, reports, rollback, over-posting, and role access.

The corrected performance scripts were executed against a synthetic LocalDB catalog and their actual plan/statistics observations are documented in [PERFORMANCE.md](../PERFORMANCE.md). The temporary performance catalog and test databases are absent. `ApexPostDb` remains present and was not a test target.

## Remaining work and limitations

1. Provision and verify a least-privilege runtime principal in each deployment environment.
2. Extend SQL-backed HTTP tests to cover duplicate-key and optimistic-concurrency conflict responses.
3. Review optional Unit of Work usage in non-sale write workflows if the requirement expands to a global transaction boundary.
4. Treat the LocalDB performance results as synthetic, single-run evidence; capture production-like workload and concurrency evidence before making capacity claims.
5. Execute the SQLCMD wrappers and two-session concurrency demonstrations only in a disposable, approved SQL environment.
