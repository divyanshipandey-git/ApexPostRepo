# Week 3 final trainer verification

> **Targeted-remediation update (2026-11-03):** The baseline verification below predates the `remediation/week3-gap-closure` branch. Current branch corrections and fresh verification are summarized in [Targeted Week 3 gap closure](#targeted-week-3-gap-closure). The tables below remain as the historical audit evidence for the original baseline.

## Targeted Week 3 gap closure

The remediation branch is based on the verified `master` commit `89d616b2da14393fb1ccb025d956bb5a19bfcdc5`. It changes the no-sales query to the requested `LEFT JOIN ... IS NULL` form; keeps low-stock recompilation disabled because the procedure has separate filtered/unfiltered paths and no measured compile-cost benefit; adds SQL-filtered/count/paginated transaction and shipment detail pages; makes the affected write services require `IUnitOfWork`; documents why `Address` is not persisted; and strengthens over-posting assertions. The schema intentionally has no row-level `ModifiedBy`: authenticated actor and action attribution is held in `AuditEntries`, while transaction `CreatedAtUtc` and selected `ModifiedAtUtc` columns remain server-managed. The SQL-backed over-posting test verifies forged creation/modification audit values are not persisted and the audit entries retain the authenticated staff identity and generated transaction IDs. No unneeded schema column was added.

`PERFORMANCE.md`, ADR-0017/0018, `DESIGN.md`, `sql/README.md`, and the repository documentation index now describe the measured scope, parameter-sensitive plan risks, permission rationale, and current SQL-backed test split. Synthetic LocalDB results remain distinct from production evidence.

Fresh SQL-enabled Release validation on this branch passed **409 tests**: 112 Domain, 216 Application (including SQL integration tests), and 81 Web integration; 0 failed and 0 skipped. The SQL test fixture used an isolated `ApexPost_SqlIT_*` catalog. Live restricted-role deployment verification and submission ZIP contents remain unverified and require external/manual evidence.

## Verification scope

This report records the Week 3 SQL remediation in this checkout, including the implementation changes, SQL-enabled tests, LocalDB performance evidence, and remaining limitations. SQL tests and synthetic measurements used disposable databases named with the required `ApexPost_SqlIT_` and `ApexPost_PerfEvidence_` prefixes. `ApexPostDb` was not used as a test or performance target.

## Requirement-by-requirement results

| Area / requirement | Implementation and change | Verification evidence | Status and remaining gap |
|---|---|---|---|
| SQL schema and deployment | EF Core migrations own schema and procedure changes; the idempotent deployment script is regenerated under `database/schema/`. Web startup does not apply migrations or call `EnsureCreated`. | SQL-enabled suite applied all seven migrations in isolated catalogs; explicit Release build succeeded. | **Verified.** Production deployment still applies migrations explicitly. |
| Safe SQL runtime and import boundary | Web composes SQL infrastructure only. JSON remains an explicit importer/test adapter and is not a runtime fallback. | Runtime-registration tests and SQL-backed Web tests pass; SQL-unavailable probe returned safe Problem Details rather than JSON-backed behavior. | **Verified.** Existing JSON compatibility paths do not acquire SQL transaction guarantees. |
| Atomic counter sale | Sale, stock reservation, payment, optional shipment, money order, and identity verification are staged through the SQL Unit of Work boundary. | SQL integration tests verify successful state through new contexts, failed-write rollback, stock restoration, shipment/order relationships, and audit persistence. | **Verified for SQL runtime.** Broader optional Unit of Work patterns in unrelated service workflows remain. |
| Stock adjustment and reversal | Stored procedure validates and atomically updates stock and movement history; void/refund restoration persists transaction transitions and stock changes. | Isolated SQL tests cover successful adjustment, invalid/negative rollback, void/refund persistence and stock state. | **Verified in LocalDB.** Production contention behavior is not established by these tests. |
| Price revision | SQL category price update uses set-based `ExecuteUpdateAsync` in the persistence boundary. | SQL-backed price-revision test passed and confirms unrelated categories are unchanged. | **Verified.** Fixed absolute category price remains an explicit business-rule assumption. |
| Daily sales and low-stock reports | SQL stored procedures perform branch/date/threshold filtering and aggregation; daily sales returns zero-valued rows for an existing branch with no sales. | SQL tests cover populated/no-sales and validation behavior. | **Verified.** LocalDB execution does not constitute a production workload test. |
| Auditor detail reports | Added SQL-filtered, counted, ordered and paginated money-order and identity-verification DTO queries and pages. Verification pages expose masked document identifiers only. | SQL-backed HTTP tests verify filters, stable pagination, date/page validation, role access, and masked display. | **Verified.** Pages are limited to Auditor and Administrator roles. |
| Top 10 products by region | Added SQL-side paid retail revenue aggregation, tax inclusion, optional branch/date filters, deterministic per-region ranking, and `/Reports/TopProducts`. | Application test, SQL repository integration assertion, and SQL-backed Auditor MVC route all pass. | **Verified.** Only retail lines linked to current product records are included. |
| Query shape and performance evidence | Added repeatable synthetic fixture and measurement scripts. Corrected the index comparison to remove/recreate the index and execute the same unhinted query. | Actual plans, logical reads, CPU/elapsed observations, estimated/actual rows, write reads, and index page usage are recorded in [PERFORMANCE.md](../PERFORMANCE.md). All seven root reporting query scripts executed successfully on the fixture. | **Verified as synthetic evidence only.** Single-run LocalDB measurements must not be generalized to production capacity or concurrency. |
| Over-posting and authorization | MVC request models keep server-owned transaction and audit properties off the writable contract; role and branch scope remain enforced. | SQL-backed request attempts to over-post IDs, status, branch, timestamps, audit fields, and monetary totals; persisted rows are re-read and verified. Manager access to auditor detail route is rejected. | **Verified for the tested counter-sale and report workflows.** |
| Exception and concurrency behavior | Middleware maps validation, not-found, conflict, and unexpected errors to safe responses; SQL Unit of Work translates rowversion and unique-key errors. | Middleware unit tests and SQL transaction tests pass. | **Partially verified.** Duplicate-key and rowversion-conflict outcomes are not exercised through a SQL-backed HTTP request. |
| Unit of Work and commit boundaries | Counter-sale SQL writes share one transaction and save; selected standalone operations have explicit commit-count tests. | Application spy tests and SQL persisted-state/rollback tests pass. | **Partial.** Optional Unit of Work dependencies and commit-count coverage remain in some non-sale write workflows. |
| Least privilege | SQL guide documents separating migration and runtime principals and grants needed for runtime operations. | Documentation reviewed; application and tests use the configured LocalDB identity. | **Deployment action remains.** A distinct restricted runtime principal was not provisioned or permission-tested here. |
| Split-query / N+1 behavior | Transaction aggregate uses a bounded split query for related collections. | SQL integration test verifies a three-reader-command load for four transactions. | **Verified for the tested repository graph.** |
| SQL demonstrations and concurrency guidance | Canonical procedures remain under `database/`; wrappers and rollback-by-default transaction/blocking demonstrations are under `sql/`. | Reporting query scripts ran on the synthetic catalog; all SQL integration tests pass. | **Partially verified.** SQLCMD wrapper execution and two-session blocking/deadlock demonstrations were not run. |
| ADR and documentation traceability | ADR-0025 contains the PKCS dependency decision; related ADR indexes and Week 3 documentation reflect the current implementation. | Updated requirement gap analysis, testing guide, rubric, README and this report. | **Verified.** The pre-existing ADR-0002 deletion and unrelated untracked assignment files are preserved. |
| Explicit exclusions | No Couchbase, CounterTerminal, offline mode, synchronization, SyncInbox/outbox, or conflict-resolution implementation was added. | Changed-file review and full tests. | **Verified.** |

## Build, tests, and database safety

The final SQL-enabled Release validation used:

```powershell
dotnet build ApexPost.sln --configuration Release
$env:APEXPOST_SQL_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=ApexPost_SqlIT_Week3Closure;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False'
dotnet test ApexPost.sln --configuration Release
```

Results:

| Project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Domain tests | 112 | 0 | 0 |
| Application tests, including SQL integration | 216 | 0 | 0 |
| Web integration tests, including SQL-backed HTTP | 81 | 0 | 0 |
| **Total** | **409** | **0** | **0** |

The explicit Release build completed with 0 warnings and 0 errors. LocalDB inventory after testing contained `ApexPostDb` and no `ApexPost_SqlIT_*` or `ApexPost_PerfEvidence_*` catalogs. The persistent `ApexPostDb` was not used for tests or performance fixture writes; its physical-only integrity check completed successfully.

## Performance evidence summary

The synthetic LocalDB run used 10 branches, 1,000 products, 10,000 stock rows, 50,000 paid transactions, and 20,000 each of shipments, money orders, and masked identity-verification records. One warm-cache run showed:

- The money-order query changed from 389 to 10 primary-table logical reads when its supporting index was present, with the optimizer choosing `IX_MoneyOrders_Branch_Created`.
- The low-stock, daily-sales, and shipment workloads showed no read reduction for their tested distributions; the optimizer retained clustered primary-key scans.
- Update and insert comparisons recorded higher measured stock-table reads with the supporting index present; both write operations were rolled back.
- Selected index storage ranged from 392 KB reserved for the stock index to 2,184 KB for the transaction index.
- Estimated and actual row counts differed materially for some workloads, highlighting fixture-specific cardinality estimation.

Full measurements, plan operators, SQL text, index page figures, and caveats are in [PERFORMANCE.md](../PERFORMANCE.md). They are actual LocalDB observations, not production throughput or capacity guarantees.

## Remaining deployment and scope limitations

1. Provision and verify a least-privilege SQL runtime principal in the deployment environment.
2. Add SQL-backed HTTP coverage for duplicate-key and optimistic-concurrency conflict responses.
3. Verify the contents of any submission ZIP externally; no ZIP archive is present in this repository.
4. Run SQLCMD wrapper and two-session concurrency demonstrations only in an approved disposable SQL environment.
5. Capture production-like query and concurrency measurements before making production-capacity claims.

See the [Week 3 requirement gap analysis](WEEK3-REQUIREMENT-GAP-ANALYSIS.md) for the corresponding traceability table.
