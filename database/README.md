# Database scripts

This folder is the SQL entry point for maintainers. The application remains SQL Server/EF Core only; Couchbase, offline operation, and synchronization are intentionally out of scope.

## Folder purpose

- `schema/`: generated schema deployment script.
- `migrations/`: pointers and deployment notes for the EF migration source.
- `queries/`: parameterized report invocations and read-only report queries.
- `stored-procedures/`: standalone `CREATE OR ALTER` copies of procedures owned by EF migrations.
- `indexes/`: read-only index verification and the evidence-based index rationale.
- `seed/`: documents reference-data seeding managed by migrations; no separate seed script is needed.
- `performance/`: reproducible synthetic workload setup and measurements.
- `verification/`: read-only schema, migration, and row-count checks.

## Authoritative sources and deployment order

EF migration classes under `src/ApexPost.Infrastructure/Persistence/Migrations/` are authoritative for schema, indexes, reference rows, and procedure definitions. The standalone procedure files mirror the final procedure bodies from those migrations for review and manual inspection; do not deploy both a migration and a standalone copy as separate sources of truth.

1. Configure `ConnectionStrings:ApexPost` with user-secrets in Development.
2. Review pending migrations, then apply them with `dotnet ef database update --project src/ApexPost.Infrastructure --startup-project src/ApexPost.Web`.
3. Alternatively, review and execute `schema/ApexPostDb-idempotent.sql` using a deployment identity.
4. Run `verification/VerifyApexPostDb.sql` and the read-only report queries.

The EF migrations remain in the Infrastructure project so `dotnet ef` discovery, migration history, and rollback metadata continue to work. `database/migrations/README.md` documents their ownership. `schema/ApexPostDb-idempotent.sql` is generated from those migrations and can be regenerated with:

```powershell
$env:APEXPOST_SQL_CONNECTION = '<design-time connection string>'
dotnet ef migrations script --idempotent --project src/ApexPost.Infrastructure --startup-project src/ApexPost.Web --output database/schema/ApexPostDb-idempotent.sql
```

Script generation creates SQL but does not connect to or modify the named database. The Web application does not migrate at startup and does not call `EnsureCreated`.

## Procedure calls and permissions

`SqlReportDataSource` invokes `dbo.usp_DailyBranchSales` and `dbo.usp_LowStockReport` with EF Core interpolated parameters. `SqlInventoryRepository` invokes `dbo.usp_AdjustStock` the same way. User values are never concatenated into SQL. The application identity needs `EXECUTE` on these three procedures and only the required `SELECT`, `INSERT`, `UPDATE`, and `DELETE` permissions on mapped tables used by its workflows. Use a separate deployment identity for schema changes; the runtime identity should not receive `db_owner` or migration permissions.

The application still performs some mapped-table queries and writes through EF, so procedure-only access is insufficient. Determine table grants from the current repository methods and deployment environment; do not use a blanket database-owner role for the runtime login.

## Rerun safety

EF migrations are tracked in `__EFMigrationsHistory`; applying them again through EF is safe. The generated idempotent script guards migration operations by migration ID. Standalone stored-procedure scripts use `CREATE OR ALTER` and can be rerun, but running them outside migration deployment does not update migration history. Report queries and verification scripts are read-only. Performance setup is destructive to its explicitly named isolated performance catalog; inspect its guard and never point it at `ApexPostDb`.

The project has no separate SQL seed script: the migration `SeedShippingZonesAndRates` supplies reference zones and rates. Operational shipments, money orders, verifications, and stock movements are created by application workflows, not fabricated to make reports look populated.

## Report verification

Use the scripts under `queries/` to verify sales splits, low stock, shipping counts, and auditor aggregates. Empty result sets are valid and should remain empty when no matching business activity exists. Do not include full identity document numbers in report queries or output.
