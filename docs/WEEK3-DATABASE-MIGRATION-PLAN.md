# Week 3 Phase 1: JSON-to-SQL Migration Plan

**Status:** Phase 1 planning snapshot; superseded for implementation status by [WEEK3-EF-IMPLEMENTATION.md](WEEK3-EF-IMPLEMENTATION.md). The plan below records the original sequencing and safety constraints; Phase 2 has since implemented and applied migrations as described in the implementation note.

## Source-to-target mapping

| JSON source | SQL target | Mapping and validation |
|---|---|---|
| `branches.json` | `Branches` | `Code.Value -> BranchCode`; opening/closing times -> SQL `time`; active status retained. Validate unique code and valid open/close interval. |
| `counters.json` | `Counters` | Preserve counter ID and name; unwrap branch-code value; validate branch FK. |
| `staff.json` | `Staff`, `Roles` | Preserve ID, name, email, current role code, home branch and active flag. Seed the four enum roles first. Validate branch/role references. Do not normalize email beyond a separately approved rule. |
| `staff-credentials.json` | `StaffCredentials` | Preserve the encoded PBKDF2-SHA256 string byte-for-byte as text; it includes algorithm/iteration/salt/hash components. Never export the raw value to reports, logs, seed output, or audit entries. Restrict DB access and backups. Validate that the existing hasher can verify imported hashes. |
| `products.json` | `Products`, `ProductCategories` | Unwrap SKU; preserve name, decimal amount, category and tax category codes. Validate money scale/range and category values. Do not change product names/prices to make reports join. |
| `inventory.json` | `BranchStock` | Composite key `(BranchCode.Value, Sku.Value)` and integer quantity. Validate each branch/SKU FK and `Quantity >= 0`; preserve exact quantity. |
| `counter-transactions.json` | `CounterTransactions`, `TransactionLines`, `Payments` | Preserve transaction/status/reason, branch/counter/clerk, money totals, payment method, line number/order, snapshots, tax categories and serialized transaction tax-rate snapshots. Unwrap value objects. `OccurredAtUtc` is null for all 40 rows currently inspected; leave null. Verify persisted subtotal/tax/total against snapshots and quarantine mismatches rather than silently recomputing. |
| `shipments.json` | `Shipments`, `ShippingZones`, `ShippingRates` | The file is absent in the inspected data directory; this is an absent input, not proof of zero history. Rates/zones currently come from Web configuration, not JSON. Load configuration as reference data only after validating the same unique `(zone, service level)` rule; report absent shipment data explicitly. |
| `money-orders.json` | `MoneyOrders`, `IdentityVerifications` | The file is absent in the inspected directory. For future source rows, unwrap money, preserve sender/recipient, branch/transaction/time, verification type/result/time, and masked number only. Require the exact mask format and never synthesize missing identity fields. |
| `audit.jsonl` | `AuditEntries` | Read one JSON object per line and preserve original event timestamp, actor/role/branch snapshots, action, safe route IDs, status, duration and correlation ID. Import only validated action-record fields. Keep append-only. Do not import request bodies or secrets; never persist full identity numbers. |
| `.apexpost-demo-seed.json` | No target table | Preserve this local manifest as a source-side integrity aid. It is not business data and should not be copied into the application database. |

`apexpost.txt` and `ApexPostWeek2.txt` are assignment files, not application records. Do not import or commit them.

## Ordered implementation phases (after design approval)

1. **Approve schema and decisions.** Resolve the open questions below, confirm any Week 3 rubric, approve whether `StockMovements` is required, and freeze field sizes/constraints. Keep all changes on a feature branch.
2. **Prepare isolated environments.** Confirm LocalDB instance availability and backup destination. Add EF Core 10 SQL Server and Design packages at aligned 10.0 servicing versions only after approval. Supply `ConnectionStrings:ApexPostDb` through Web user-secrets in Development; do not commit it. Keep production/staging values in deployment secret configuration. Name the local application database `ApexPostDb`.
3. **Create EF model and schema.** Add Infrastructure-owned DbContext, Fluent mappings and the Application `IUnitOfWork` contract. Create reviewed EF migration source files, but do not apply them automatically at Web startup. Add a separate disposable integration-test database configuration.
4. **Build a read-only importer.** Implement a one-time tool that takes an explicit source directory and destination connection. It must open source files read-only; never seed, rewrite, rename, or delete them. Validate all collections before target writes and print only counts, filenames, safe error categories and aggregate discrepancies.
5. **Dry run and reconcile.** Create a fresh empty target DB only in the approved migration operation. Validate duplicate keys, missing FKs, malformed values, status/enums, amounts, line snapshots, timestamps and masked identities. Produce a machine-readable import manifest containing file names, source SHA-256, row counts, target counts and the importer version; keep secrets and identity values out of it. Stop on any unexplained mismatch.
6. **Load in dependency order.** In a single migration run, load role/category/zone reference rows; branches, counters, staff/credentials and products; stock; transactions and dependent lines/payments; shipments/orders/verifications when source files exist; then audit entries. Use explicit keys and transaction batches. Store unknown legacy timestamps as null. Keep source manifest hashes and per-table counts for repeatable verification.
7. **Run parity and regression tests.** Compare source/target counts and safe aggregates (e.g. totals/counts by status and category), validate FKs and constraints, authenticate against imported credential hashes, compare report outputs where timestamps exist, and test reversals and concurrency against SQL. Use a temporary test database; never point tests at the developer `ApexPostDb` or `App_Data`.
8. **Switch one environment at a time.** Change only the configured persistence adapter after parity is approved. Do not dual-write JSON and SQL: partial dual writes make the source-of-truth ambiguous. Retain the original JSON snapshot read-only with verified hashes. Keep rollback available before the first SQL business write.
9. **Operate and retain evidence.** Monitor SQL exceptions, rowversion conflicts, audit append failures, and transaction rollback rates. Record actual query plans, logical reads and CPU before any index claim. Preserve migration reports and reviewed SQL scripts under data-governance controls, not personal data.

## Data preservation and rollback

- Before future migration, make a byte-for-byte read-only backup copy of every original data file and record SHA-256 hashes, size and timestamp metadata. Verify the backup by recomputing hashes. Do not let the importer modify either original or backup.
- Never use the seed tool as a migration tool. Never run the seed tool against existing `App_Data` as part of database work.
- Import to an empty, separately named target. A failed run must roll back its SQL transaction or discard the isolated target database after preserving diagnostics; it must not change JSON.
- Keep a row-level error report with opaque source file/collection plus safe record ordinal/key hash where necessary. Do not include email, password hashes, names, masked document numbers, or raw JSON values in ordinary logs.
- Before SQL cutover, rollback means restoring the prior JSON configuration and continuing with unchanged JSON files. After SQL accepts writes, switching back to stale JSON would lose those writes. At that point, rollback needs a planned SQL-to-JSON delta/reconciliation or a restore point and an explicit business decision. Do not promise a transparent rollback after writes begin.
- No automatic timestamp repair: existing transactions without `OccurredAtUtc` remain unknown and excluded from date-based reports. Future operators may make a separately approved data correction only from reliable external evidence, with a documented audit trail.

## Repository and Unit of Work implementation sequence

1. Add `IUnitOfWork` to Application with an async commit operation and cancellation token. Keep EF types out of Application and Domain.
2. Register one scoped `ApexPostDbContext`; every Infrastructure repository and Unit of Work in that scope must resolve the same context.
3. Convert repository mutations to stage tracked changes only. Repository methods must never call `SaveChangesAsync`. Keep `IQueryable` private to Infrastructure.
4. Map simple aggregate types with EF Fluent API/value converters. Add a small Infrastructure persistence model/mapper for transaction lines, captured tax rates, payment and nested identity data where the current domain shape is awkward for relational keys. Keep the domain model and behavior intact.
5. Refactor each application use case in a small, separately reviewed step so one successful use case commits once. Counter sale commits stock decrement, transaction, lines, payment, optional shipment and optional money order/verification together. Void/refund commit state transition and stock restoration together. Stock adjustment commits its stock row and movement record together. Notifications occur after commit.
6. Add minimal specialized inventory operations for atomic reserve/adjust/reversal; avoid read-then-write races. Use conditional SQL update or rowversion and check constraints. Map EF `DbUpdateConcurrencyException` to an explicit retry/conflict result, not a silent overwrite.
7. For reports and list screens, add bounded query methods returning DTOs and use `AsNoTracking` projections. Keep service behavior and output contract stable. Do not fetch full tables into memory just to preserve JSON-era implementation details.
8. Keep `IAuditStore` outside the business Unit of Work by default. SQL audit insertion is append-only and separately handled; a failure is logged and reported as an audit failure without falsely claiming success or undoing a committed sale. If a future security requirement mandates fail-closed audit, obtain explicit approval and design that transaction boundary before coding.

## Reversal retry guard: preserve behavior, strengthen durability

Today, `TransactionVoidService` uses a process-wide per-transaction semaphore and `JsonInventoryRepository` remembers restored transaction IDs in a process-local set. This suppresses concurrent/repeated in-process restoration and covers a transaction-write failure retry while that process stays alive. It cannot survive a process crash between inventory and transaction files, nor coordinate separate processes.

Under SQL, put the Paid-to-Voided/Refunded state change, stock quantity updates, and one unique `StockMovements` reversal record per transaction and reversal type in one database transaction. The transaction's `rowversion` arbitrates concurrent state transitions; stock updates must be conditional/nonnegative and protected by `BranchStock.rowversion` or an atomic conditional update. A database unique constraint on the reversal source prevents a replay from restoring twice. If all writes are in one transaction, a failure rolls them all back, so process-local marker state is no longer the correctness boundary. Keep a per-transaction in-process gate only if it simplifies local contention; correctness must not depend on it.

## LocalDB, SDK, packages and configuration findings

**Verified on this machine:** `global.json` requests SDK `10.0.100` with `latestFeature` roll-forward; installed SDKs include `10.0.401` and `10.0.402`; project TFM is `net10.0`, nullable enabled, C# `14.0`, warnings treated as errors. Global `dotnet-ef` is `10.0.14`. `SqlLocalDB.exe` and `sqlcmd` are installed and `sqllocaldb info` lists `MSSQLLocalDB`. No database was queried or created. These machine observations do not guarantee LocalDB on another developer machine or CI agent.

**Current package state:** There is no EF Core package reference in the projects or lock files observed. Existing direct packages include `Microsoft.Extensions.DependencyInjection` 10.0.13, `Microsoft.AspNetCore.Mvc.Testing` 10.0.0, xUnit 2.9.3, and test SDK 17.14.1. No EF package conflict was found because EF is not installed. EF Core 10 targets/requires .NET 10, so it is compatible with the current `net10.0` target. Future SQL provider and EF Design package versions should use a mutually aligned EF 10 servicing version; provider/tool major versions must match EF Core major version. Add the SQL Server provider and Design-time package only after Phase 1 approval. Do not install/upgrade packages in this phase.

Current setup uses the Web User Secrets ID in Development and key `ConnectionStrings:ApexPost`. The design-time factory and import/diagnostic tool load that secret; `APEXPOST_SQL_CONNECTION` is an explicit environment override. CI and opt-in SQL tests must use isolated databases named with the `ApexPost_SqlIT_` prefix, never the developer database. LocalDB is a Windows development convenience and must not be assumed as a production hosting choice.

Official compatibility references: [EF Core 10 what's new and support lifecycle](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew), [EF Core releases and target frameworks](https://learn.microsoft.com/en-us/ef/core/what-is-new/), [SQL Server EF Core provider](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/), and [SQL Server Express LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb).

## Verification plan for the later implementation

- Keep the current 352-test suite unchanged as a regression baseline; run the full suite after each repository/service migration step.
- Add Domain tests only for any independently approved domain behavior changes; none are expected from this database design.
- Add Infrastructure integration tests for each mapping, key conversion, enum/check/FK, masked identity validation, transaction commit/rollback, append-only audit, and optimistic concurrency behavior.
- Add Application tests proving one use case commits once, no repository saves independently, sale/reversal atomicity, safe retry outcomes, notification-after-commit behavior, and correct failure mapping.
- Run Web integration tests against isolated LocalDB/test database configuration for login, branch authorization, sales, void/refund, stock, money orders, audit, and reports. Do not run tests against local demo `App_Data`.
- Use a sanitized fixture derived from schema only, not copied personal data. Separately run importer dry-run against a preserved source copy under controlled local access and verify file hashes before/after.
- Collect SQL execution plans, logical reads and CPU for each proposed workload with representative data. Treat index candidates as hypotheses until measured.

## Performance artifact boundary

The repository has `docs/performance-measurement.md`, which records a bounded product-cache illustration, but no root `PERFORMANCE.md`. It was not modified. This phase creates no new performance measurements or claims.
