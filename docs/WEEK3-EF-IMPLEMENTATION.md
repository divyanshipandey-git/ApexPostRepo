# Week 3 EF Core implementation notes

## Provider and migrations

The Web runtime registers EF Core 10 SQL Server repositories in `Program.cs`; JSON repositories remain available through explicit JSON infrastructure registration used by legacy tests and import tooling, but are not registered by Web startup. There is no checked-in connection-string fallback. In Development, configure `ConnectionStrings:ApexPost` through Web user-secrets; runtime startup and EF tooling fail with an actionable message when it is absent. `APEXPOST_SQL_CONNECTION` is an explicit environment override for tooling/automation. Migrations are applied explicitly; startup does not call `EnsureCreated` or apply migrations automatically.

The infrastructure project contains `InitialCreate`, `AddSqlReportingAndStockProcedures`, `SeedShippingZonesAndRates`, `GuardStockAdjustmentOverflow`, `UpdateDailyBranchSalesProcedure`, and `AddReportFilters`. The latest migration updates report procedure parameters and adds explicit stock-adjustment validation/rollback. Review and apply with:

```powershell
dotnet ef migrations script --idempotent --project src/ApexPost.Infrastructure --startup-project src/ApexPost.Web --output docs/sql/ApexPostDb-idempotent.sql
dotnet ef database update --project src/ApexPost.Infrastructure --startup-project src/ApexPost.Web
```

The checked-in idempotent script creates schema, reference lookup values, reporting procedures, and the stock adjustment procedure. It contains no database drop/reset operation.

## Persistence boundaries

`ApexPostDbContext` maps the current domain records and SQL-only transaction, payment, stock movement, audit, and importer records. Application repositories return domain objects or report DTOs; no `IQueryable` escapes Infrastructure. `IUnitOfWork` provides one save per grouped business write and an explicit transaction helper for operations that combine EF with SQL commands. Rowversion columns protect stock and transaction updates.

Counter sales stage the transaction, payment, shipment/money order, and stock decrement in one scoped context before saving. Void/refund stage the status transition, stock restoration, and unique reversal movement together. The database unique movement index and rowversion guard retries across requests/processes; process-local gates remain an optimization only. SQL repository and unit-of-work registrations share the scoped context. The product catalogue cache is scoped as well because it retains the scoped repository.

`usp_AdjustStock` requires a reason and actor and writes the quantity change and stock movement atomically. Its caller uses interpolated parameters through EF. `usp_DailyBranchSales` and `usp_LowStockReport` provide set-based reporting. `SqlReportDataSource` executes both procedures and maps them into application report DTOs; the daily procedure separates retail and service totals.

## Read-only JSON import

The importer supports a read-only preview and a separately invoked import:

```powershell
dotnet run --project tools/ApexPost.DemoSeed -- --import-preview --data-directory <existing-json-directory>
dotnet run --project tools/ApexPost.DemoSeed -- --import-json --data-directory <existing-json-directory>
```

For a fresh, explicitly selected empty demo directory, `--demo-json-200 --data-directory <existing-empty-directory>` can generate a separate synthetic dataset with 5 branches, 20 staff, 30 products, inventory, and 200 deliberately synthetic dated transactions. It requires `DemoData:Password` from user-secrets or `APEXPOST_DEMO_DATA_PASSWORD`; the password is not embedded in source. This fixture must not be presented as historical production evidence. Use a new empty directory; keep the checked-in `App_Data` and JSON source untouched.

Import requires migrations to have been applied first. It fingerprints the listed source files, records a successful fingerprint, refuses to import into a database with business rows but no matching run record, and writes all imported rows and the run record in one database transaction. A repeated identical import is a no-op. The source directory is read only. Missing transaction timestamps remain null; the importer does not infer dates or create missing shipments/orders. Existing password hashes are validated and copied without printing their values. The existing `audit.jsonl` shape is validated and stored in `AuditEntries` with an allowlist of route keys; the source body, secrets, full identity values, and unrecognized route keys are not imported.

Use `--query-diagnostics` to capture current EF SQL translations. Actual LocalDB execution-plan XML and `SET STATISTICS IO, TIME ON` measurements for the four required report workloads and the non-SARGable comparison are recorded in [PERFORMANCE.md](../PERFORMANCE.md). The remaining caveat is that the retained synthetic distribution does not represent production hardware, concurrency, or cache pressure.

## Verification state

The idempotent deployment script was reviewed for destructive database/table operations. `ApexPostDb` remains the persistent development database, with all six migration IDs recorded; reference data includes 4 roles, 3 product categories, 10 zones, and 12 shipping rates. The SQL integration fixtures apply the same migrations to uniquely named temporary catalogs and clean those catalogs up automatically.

The inspected `App_Data` snapshot contains 5 branches, 5 counters, 20 staff/credentials, 30 products, 150 stock rows, 40 transactions, and 102 audit events; it has no shipment or money-order file. The full business import is not rerun against the existing database because its business snapshot differs. Instead, the explicit audit-only command imported the 102 validated historical events into SQL without changing business records; repeating that command is a no-op. The JSON source files were not modified.

`ApexPostDb` contains 5 branches, 5 counters, 24 staff/credentials (20 business records and 4 local-only role test accounts), 30 products, 150 stock rows, 10 zones, 12 rates, 200 transactions, 200 transaction lines, 200 payments, 800 transaction tax-rate rows, 4 roles, 3 categories, one full-import ledger row, and 102 audit entries. The transaction dataset is the existing synthetic development dataset, not historical production evidence. `IdentityVerifications`, `MoneyOrders`, `Shipments`, and `StockMovements` currently have zero rows: each is workflow-driven and is covered by isolated SQL integration workflows rather than artificial persistent seed data.

The daily-sales and low-stock procedures executed successfully. `usp_AdjustStock` was exercised with temporary branch/product rows inside an outer transaction that was rolled back; stock and movement outputs matched. The overflow guard rejected an excessive delta with SQL error 51004 and left the existing quantity and movement history unchanged. SQL repository reporting reads and SQL-backed MVC workflows were exercised against isolated test catalogs.

The opt-in SQL Server integration suite uses `APEXPOST_SQL_TEST_CONNECTION` and refuses catalogs without the `ApexPost_SqlIT_` prefix. It applies all six EF migrations and covers repository create/update/read, unit-of-work commit/rollback, sale consistency, concurrent last-unit sales, stale stock updates and duplicate reversal restoration across separate contexts, void/refund transitions, SQL audit persistence and repeat-safe audit import, stock adjustment, filtered stored-procedure report results, shipment/money-order/identity persistence, database-priced sales, and invalid/repeated full JSON imports. Focused SQL-backed MVC tests cover login and reads, sale persistence, void authorization, stock restoration, and audit persistence. Temporary test databases are automatically removed by the fixtures.

The local-development login accounts are separate `LOCALDEV-*` SQL staff/credential rows created only by `--provision-local-test-accounts` in Development against Windows-authenticated LocalDB. Their four independent passwords are kept in the Web project's user-secrets store; the tool validates the stored hashes through `StaffAuthenticationService` and does not print passwords or hashes. The setup and role/screen matrix is in [DATABASE-TESTING-GUIDE.md](DATABASE-TESTING-GUIDE.md).

Import tests create disposable generated JSON input under the system temporary directory; the Web runtime does not read App_Data. A separate LocalDB performance catalog contains synthetic shipment and money-order workloads; its actual-plan XML, IO/time output, forced-primary-key baselines, optimizer-path measurements, and non-SARGable comparison are documented in [PERFORMANCE.md](../PERFORMANCE.md). The remaining limitation is production representativeness, not missing measurement artifacts.

## Completion status

The Week 3 SQL Server database implementation is functionally complete. The Web runtime uses SQL Server only; EF migrations, repositories, unit-of-work transactions, audit persistence, stored procedures, filtered SQL-backed reporting, MVC workflows, and concurrency safeguards are implemented and covered by the full 390-test suite (112 Domain, 201 Application, and 77 Web integration tests, including the opt-in SQL Server tests). Couchbase and offline synchronization remain outside this assignment scope.

`PERFORMANCE.md` contains actual plans, IO/time output, and measured before/after access paths for all requested synthetic workloads. The remaining limitation is representativeness: these LocalDB results are not production-capacity evidence and should be repeated against an approved anonymized production-like distribution before capacity or index decisions.
