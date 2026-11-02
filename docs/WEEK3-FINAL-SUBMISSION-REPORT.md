# Week 3 Final Submission Report

## 1. Assignment overview and objectives

This submission covers the Week 3 SQL remediation for ApexPost Counter Platform. The work is on branch `feature/week3-sql-gap-remediation` and was validated with the full solution test suite using `APEXPOST_SQL_TEST_CONNECTION` configured to an isolated SQL Server LocalDB test database.

The objective was to preserve the Week 2 business rules while completing the SQL-backed reporting, transaction/error handling, EF behavior, and documentation gaps. The implementation does not introduce Couchbase, offline sync, SyncInbox, or conflict resolution.

The repository remains aligned with the Week 2 requirement to preserve the application architecture: domain-first entities, application abstraction interfaces, infrastructure implementations, and an MVC web composition root.

## 2. Scope and implemented features

Implemented in the current solution:

- SQL Server-first runtime using EF Core 10 and LocalDB-compatible SQL Server configuration.
- `ApexPostDbContext` with Fluent API entity configuration and value-object conversions.
- Shared `IUnitOfWork` and SQL repository implementations behind the existing application interfaces.
- Code-first migrations with idempotent schema generation.
- JSON snapshot import into an empty SQL database with duplicate-import protection.
- Stored procedures for daily branch sales, low-stock reporting, and stock adjustments.
- Date/branch/threshold report filters with SQL-side aggregation and role-aware branch scoping.
- Administrator-only category price revision using a set-based SQL update.
- Explicit transaction boundaries and concurrency handling.
- Safe concurrency Problem Details and request-cancellation handling.
- SQL-backed MVC business flows and audit persistence.
- Tests for SQL persistence, web integration, and end-to-end validation.
- Performance evidence preserved in the repository and documented in the performance files.

Excluded from this work and intentionally not implemented:

- Couchbase Lite / offline-first counter terminal
- SyncInbox and synchronization semantics
- Conflict resolution or outbox patterns
- Any additional production refactoring outside the SQL persistence work

## 3. Architecture overview

The solution structure reflects the intended dependency direction:

- Domain: value objects, entities, invariants
- Application: service layer, ports, validation, report DTOs, domain abstractions
- Infrastructure: SQL Server persistence and repository implementations
- Web: MVC controllers, views, security, filters, middleware, composition root

Key implementation files:

- `src/ApexPost.Infrastructure/Persistence/ApexPostDbContext.cs`
- `src/ApexPost.Infrastructure/Persistence/ApexPostEntityConfigurations.cs`
- `src/ApexPost.Infrastructure/Persistence/SqlInfrastructureExtensions.cs`
- `src/ApexPost.Infrastructure/Persistence/SqlUnitOfWork.cs`
- `src/ApexPost.Infrastructure/Persistence/Repositories/SqlRepositories.cs`
- `src/ApexPost.Web/Program.cs`
- `src/ApexPost.Web/DependencyInjection.cs`

## 4. Database design and important tables

The implemented database is `ApexPostDb` and remains the single persistent development database. Temporary SQL integration databases are created only under the `ApexPost_SqlIT_*` naming convention and are cleaned up automatically by the fixtures.

Important tables implemented via EF Core include:

- `Branches`
- `Counters`
- `Staff`
- `StaffCredentials`
- `Roles`
- `ProductCategories`
- `Products`
- `BranchStock`
- `Shipments`
- `ShippingZones`
- `ShippingRates`
- `CounterTransactions`
- `TransactionLines`
- `TransactionTaxRates`
- `Payments`
- `MoneyOrders`
- `IdentityVerifications`
- `StockMovements`
- `AuditEntries`
- `ImportRuns`

Implementation evidence:

- `src/ApexPost.Infrastructure/Persistence/ApexPostEntityConfigurations.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261031173410_InitialCreate.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261031173907_AddSqlReportingAndStockProcedures.cs`

Key design choices:

- `BranchCode` and `Sku` value objects are converted to database strings.
- Money and weight are stored as decimals/typed values.
- Audit columns use `CreatedAtUtc` and `CreatedBy` with `ModifiedAtUtc` where needed.
- Rowversion/concurrency semantics are enforced in the inventory and transaction flows by optimistic concurrency and database-level safeguards.
- Branch stock, identity verification, and reporting tables are modeled to preserve the original Week 2 domain rules.

## 5. JSON-to-SQL import approach

The solution retains a migration path from the original JSON business data snapshot into a SQL-backed database without changing the business domain. The importer is implemented in:

- `src/ApexPost.Infrastructure/Persistence/JsonDataImporter.cs`

The importer:

- reads the previous JSON snapshot from a selected directory
- hashes the file set to create a source fingerprint
- rejects re-import if the source fingerprint already exists
- blocks import if the target database already contains business data without a matching import record
- wraps the workload in an execution strategy and database transaction
- records the import in `ImportRuns`
- provides a separate repeat-safe audit-only import for a validated legacy `audit.jsonl` when the business database already contains a different snapshot

This protects the database from accidental duplicate imports and preserves reproducible data migration evidence.

The current `App_Data` directory previews to 40 transactions, no shipment or money-order files, and 102 audit events. The existing SQL ledger records a different 430-row business import containing 200 transactions. The full importer correctly refuses to merge that mismatched snapshot into populated `ApexPostDb`; no business rows were replaced. The 102 validated audit events were imported through the audit-only path and recorded as a separate import run.

## 6. EF Core Code First and migrations

The project uses EF Core Code First and migrations rather than database-first generation. The model is configured through `DbContext` and Fluent API and deployed through generated migration files.

Key files:

- `src/ApexPost.Infrastructure/Persistence/ApexPostDbContext.cs`
- `src/ApexPost.Infrastructure/Persistence/ApexPostEntityConfigurations.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261031173410_InitialCreate.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261031173907_AddSqlReportingAndStockProcedures.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261101010000_UpdateDailyBranchSalesProcedure.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261102000000_AddReportFilters.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261103000000_HardenReportingProcedures.cs`
- `database/schema/ApexPostDb-idempotent.sql`

The migration history includes:

- initial schema creation
- shipping-zone and rate seed data
- SQL reporting and stock adjustment procedures
- stock-adjustment overflow guard
- report procedure update and validation tightening
- date/branch and low-stock threshold report filters, plus transactional stock procedure rollback handling
- hardened reporting procedures and rollback definitions

The idempotent SQL script is maintained under `database/schema/ApexPostDb-idempotent.sql`.

## 7. Repository and Unit of Work implementation

The application ports remain the same, but the infrastructure implementation is SQL-based. Repositories are implemented in:

- `src/ApexPost.Infrastructure/Persistence/Repositories/SqlRepositories.cs`

Repository responsibilities:

- stage entity changes but do not call save directly
- use the scoped `ApexPostDbContext`
- return domain entities and aggregate snapshots rather than `IQueryable`

`SqlUnitOfWork` is implemented in:

- `src/ApexPost.Infrastructure/Persistence/SqlUnitOfWork.cs`

It provides:

- `SaveChangesAsync()` with concurrency exception translation to `ConcurrencyConflictException`
- `ExecuteInTransactionAsync()` using the EF Core execution strategy and explicit transaction boundaries

## 8. Stored procedures and reporting

Stored procedures are created as SQL Server procedures and invoked through EF Core parameterized APIs. The implemented procedures include:

- `dbo.usp_DailyBranchSales`
- `dbo.usp_LowStockReport`
- `dbo.usp_AdjustStock`

Evidence:

- `src/ApexPost.Infrastructure/Persistence/Migrations/20261031173907_AddSqlReportingAndStockProcedures.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261101010000_UpdateDailyBranchSalesProcedure.cs`
- `src/ApexPost.Infrastructure/Persistence/Repositories/SqlRepositories.cs`

The reports also use the SQL-backed `IReportDataSource` implementation, which is captured in the application service layer and used by the reporting screens.

The report page accepts date, branch, and validated threshold input. Branch Managers remain restricted to their home branch; Auditors and Administrators may filter across branches. Shipping and branch service activity are filtered and grouped in SQL. The current report query translations are recorded in `PERFORMANCE.md`; actual-plan measurements for the changed filters remain an explicit follow-up before production capacity claims.

## 9. Transaction and concurrency handling

The SQL implementation preserves the business logic and adds stronger data integrity.

Evidence:

- `src/ApexPost.Infrastructure/Persistence/SqlUnitOfWork.cs`
- `src/ApexPost.Application/Services/Transactions/CounterSaleService.cs`
- `src/ApexPost.Infrastructure/Persistence/Repositories/SqlRepositories.cs`

Examples:

- explicit transaction boundaries around shared persistence and stored-procedure operations
- concurrency conflict translation using `DbUpdateConcurrencyException`
- stock reservation and restoration logic for transaction rollback scenarios
- database validation constraints to guard negative movement and invalid stock changes

## 10. SQL-backed application workflows

The Web runtime is SQL-only through the application composition root:

- `src/ApexPost.Web/Program.cs`
- `src/ApexPost.Infrastructure/Persistence/SqlInfrastructureExtensions.cs`

The application continues to support Week 2 business flows such as:

- Branch and counter management
- Staff authentication and authorization
- Product and stock inquiry
- Shipping and money-order workflows
- Counter transaction creation, voids, refunds and audit trail
- reporting screens and dashboard summaries

The design keeps controllers thin and delegates business logic to application services and repositories.

## 11. Security implementation

Security remains aligned with the Week 2 requirements and is implemented in the Web project:

- cookie authentication and role claims
- authorization policies such as manager-only and same-branch rules
- antiforgery tokens on form posts
- secure cookie settings
- rate limiting for login and partner integration
- middleware for maintenance mode, correlation ID, structured timing, safe exception handling, and security headers
- API-key metadata for integration endpoints

Evidence:

- `src/ApexPost.Web/DependencyInjection.cs`
- `src/ApexPost.Web/Program.cs`
- `src/ApexPost.Web/Security/*`
- `src/ApexPost.Web/Middleware/*`
- `src/ApexPost.Web/Controllers/CourierIntegrationController.cs`

## 12. Performance optimization and measured results

The implementation preserves all recordable performance evidence in the repository and includes workload analysis across actual SQL Server plans and runtime metrics.

Performance files:

- `PERFORMANCE.md`
- `database/performance/performance-evidence-setup.sql`
- `database/performance/performance-evidence-measurements.sql`
- `docs/performance-plans/actual-plans-and-statistics.txt`

The performance documentation records corrected synthetic LocalDB measurements for low stock, daily sales, shipment search, and money-order review, and explicitly distinguishes those results from production capacity claims.

## 13. Testing strategy and final results

The final validation used the configured SQL integration environment. The full suite was executed after setting `APEXPOST_SQL_TEST_CONNECTION` to the SQL Server LocalDB test database.

Final result for the latest SQL-enabled Release run:

- 407 passed
- 0 failed
- 0 skipped
- total: 407

Project totals: 112 Domain, 214 Application, and 81 Web integration tests. The explicit Release build succeeded with 0 warnings and 0 errors.

Key test files:

- `tests/ApexPost.Domain.Tests/**/*.cs`
- `tests/ApexPost.Application.Tests/Persistence/SqlServerIntegrationTests.cs`
- `tests/ApexPost.Web.IntegrationTests/SqlWebApplicationIntegrationTests.cs`

The SQL test fixtures create temporary `ApexPost_SqlIT_*` databases, apply migrations, run the integration suite, and clean the database up automatically thereafter. The final LocalDB check found no `ApexPost_SqlIT_*` databases. `ApexPostDb` remains the single persistent development database.

## 14. Known limitations

The implementation remains intentionally aligned with the Week 3 assignment and excludes the explicitly excluded features:

- no Couchbase/offline-first implementation
- no SyncInbox or outbox-based reconciler
- no conflict resolution flow
- no database changes outside the SQL Schema and persistence work

The repository also preserves the original Week 2 security and business invariants rather than simplifying them. The design remains intentionally conservative and assignment-fit.

## 15. Database population and runtime verification

### Empty-table findings and actions

| Table | Expected data source | Actual reason for initial emptiness | Required action | Verification |
|---|---|---|---|---|
| `AuditEntries` | Imported historical `audit.jsonl` and the Web `AuditActionFilter` | The SQL business snapshot did not include the current App_Data audit file, and no audited SQL-backed POST had run | Imported the 102 validated existing events with a repeat-safe audit-only transaction; new actions use `SqlAuditStore` | 102 SQL rows; second identical import reported already imported; integration test verifies audit persistence |
| `IdentityVerifications` | Nested verification only when a money-order policy requires it | No money orders existed in the existing SQL data and no `money-orders.json` file exists | No initial data; created only by a successful supported money-order workflow | SQL application/integration workflow verified identity-to-order relationship and masked value |
| `MoneyOrders` | Supported money-order workflow | No `money-orders.json` source and no persistent SQL workflow had issued one | No seed rows; do not fabricate one | SQL MVC and application integration workflows persist and read money orders |
| `Shipments` | Supported shipment workflow, including a shipment line on a counter sale | No `shipments.json` source and no persistent SQL workflow had created one | No seed rows; do not fabricate one | SQL MVC and application integration workflows persist shipment-to-transaction links |
| `StockMovements` | `usp_AdjustStock` and successful void/refund restoration | Existing opening stock balances have no movement history; no adjustment or reversal occurred in `ApexPostDb` | No opening movement rows synthesized; workflow records are verified in isolated SQL integration databases | SQL integration tests verify adjustment and reversal movements |

Identity verification, money order, shipment, and stock movement tables are workflow-driven, not minimum seed tables. The supported workflows succeeded in SQL integration tests, but their test rows were confined to temporary databases and cleaned up. No fake business transactions were created in `ApexPostDb`.

### Persistent row counts

Read-only counts taken before and after the audit import:

| Table | Before | After |
|---|---:|---:|
| Branches | 5 | 5 |
| Counters | 5 | 5 |
| Staff | 24 | 24 |
| Roles | 4 | 4 |
| Products | 30 | 30 |
| ProductCategories | 3 | 3 |
| BranchStock | 150 | 150 |
| ShippingZones | 10 | 10 |
| ShippingRates | 12 | 12 |
| Shipments | 0 | 0 |
| MoneyOrders | 0 | 0 |
| IdentityVerifications | 0 | 0 |
| CounterTransactions | 200 | 200 |
| TransactionLines | 200 | 200 |
| Payments | 200 | 200 |
| AuditEntries | 0 | 102 |
| StockMovements | 0 | 0 |

Reference-data counts meet the stated minimums: 5 branches, 20 staff minimum (24 present), 30 products, 10 zones/rates minimum (10 zones and 12 rates present), and at least 200 historical transactions (200 present). The current App_Data directory contains 20 staff and 40 transactions; it is not the same source snapshot as the existing SQL database. The SQL import ledger’s 430 count corresponds to the 200-transaction business snapshot, not to the current App_Data files. The audit-only import preserves that distinction.

### SQL connectivity and SQL-only runtime evidence

- The configured development target resolved to `(localdb)\MSSQLLocalDB` and `ApexPostDb`; a direct `sqlcmd` connection returned `DB_NAME() = ApexPostDb`.
- EF Core SQL report reads using the configured development connection returned 200 transaction rows and 150 inventory rows.
- The Web app started in Development; `/health` and `/Account/Login` returned HTTP 200. It also started with an isolated nonexistent `JsonPersistence:DataDirectory`; the actual `App_Data` directory was not renamed or deleted.
- The Web composition root invokes `AddSqlInfrastructure`. DI tests assert SQL repositories are active and JSON repositories/options are not registered. SQL-backed MVC tests read and write through the SQL repositories.
- An HTTP login POST against an intentionally unavailable SQL catalog returned HTTP 500 Problem Details with a trace/correlation ID. It did not return a JSON-backed login result.
- `JsonDataImporter` is an explicit tool-only migration path; no controller or application service reads App_Data. The SQL report smoke read and SQL-backed MVC tests continued to use SQL.
- After stopping the valid Web host, EF Core report reads and direct SQL row counts still succeeded, confirming the imported data remained in `ApexPostDb`.
- SQL constraint checking completed without errors. No temporary `ApexPost_SqlIT_*` database remained after the full suite.

Exact read-only counts and relationship queries are in [WEEK3-DATABASE-GUIDE.md](./WEEK3-DATABASE-GUIDE.md#13-read-only-ssms-verification-queries).

### Workflows and persistence evidence

The only new persistent `ApexPostDb` data from this audit is the 102 existing audit records imported from App_Data through the explicit audit-only importer, plus its import ledger entry. The importer was run twice; the first added 102 and the second was a no-op. EF Core then read the current database successfully. SQL integration tests independently verified sale, shipment, money-order/identity, audit, stock adjustment, stock reversal, rollback, and fresh-context reads. These workflow rows were automatically removed with their isolated test databases.

## 16. Conclusion

SQL connectivity and SQL-only runtime behavior are verified. The historical audit data is imported repeat-safely. The remaining four empty tables are explained and correctly remain empty in the persistent database because their operational workflows have not been executed there and no corresponding legacy JSON source files exist. All 407 tests passed, including SQL Server integration and MVC tests, with temporary test databases cleaned up. Synthetic LocalDB plan evidence is documented, but does not support production capacity claims. See the [gap analysis](WEEK3-REQUIREMENT-GAP-ANALYSIS.md) and [final trainer verification](WEEK3-FINAL-TRAINER-VERIFICATION.md) for limitations.

## 17. Final verdict

| Check | Result |
|---|---|
| SQL CONNECTION | VERIFIED |
| SQL-ONLY RUNTIME | VERIFIED |
| EMPTY TABLES | EXPLAINED |
| DATA PERSISTENCE | VERIFIED |
| TESTS | PASSED |
| SUBMISSION | READY |
