# Week 3 Database Guide

## 1. Database structure and relationships

The project’s persistent development database is `ApexPostDb`.

The relational model supports the ApexPost domain and preserves the original business rules from Week 2 while making the system-of-record database relational and queryable.

Main relational tables include:

- `Branches`
- `Counters`
- `Staff`
- `StaffCredentials`
- `Roles`
- `ProductCategories`
- `Products`
- `BranchStock`
- `ShippingZones`
- `ShippingRates`
- `Shipments`
- `MoneyOrders`
- `IdentityVerifications`
- `CounterTransactions`
- `TransactionLines`
- `TransactionTaxRates`
- `Payments`
- `StockMovements`
- `AuditEntries`
- `ImportRuns`

### Key relationships

- `Counters` belong to a `Branch`.
- `Staff` belongs to a `Branch` and a `Role`.
- `StaffCredentials` are linked to a `Staff` row.
- `Products` are categorized via `ProductCategories`.
- `BranchStock` is keyed by branch + SKU.
- `CounterTransactions` gather line items, tax rows, and payment rows.
- `Shipments` and `MoneyOrders` link to transaction and branch lifecycles.
- `AuditEntries` record security, inventory, transaction, or operational actions.
- `ImportRuns` track JSON-source imports to prevent duplicate imports.

## 2. Important tables and their purposes

### Branches
Stores branch identity and operating hours. The branch code is the primary key and stays in a compact varchar form.

### Counters
Stores counters attached to a branch, with active/inactive state and audit metadata.

### Staff and StaffCredentials
Stores staff identity, role, branch association, and secure password-hash storage in a separate table.

### Products and ProductCategories
Products are keyed by SKU and carry category, tax, and pricing metadata.

### BranchStock
The operational inventory table. It is the central source for stock validation and stock-change operations.

### CounterTransactions, TransactionLines, TransactionTaxRates, Payments
Represents the complete sale and payment lifecycle. Transactions are the aggregate root for a business basket and sale.

### MoneyOrders and IdentityVerifications
Manages money-order issuance and masked identity verification storage. The full document number is never stored in the domain and is masked before any persisted representation is accepted.

### Shipments
Tracks parcel or letter shipments, service-level information, destination zone, and associated operational tracking data.

### AuditEntries
Stores action metadata required for branch and operational auditing, including controller, action, staff, branch, status code, correlation ID, and timing details.

## 3. Primary keys, foreign keys, unique keys, and checks

Key configuration is defined in:

- `src/ApexPost.Infrastructure/Persistence/ApexPostEntityConfigurations.cs`

Examples include:

- composite or key-based unique constraints for branch-stock identities
- unique email index on `Staff.Email`
- foreign keys from counters to branches, staff to branches and roles
- check constraints for branch opening hours, stock quantity constraints, and role/category constraints
- non-empty credential hash validation

Important database invariants:

- opening hours must have valid ordering
- role code values are restricted to the allowed domain roles
- product category values are restricted to the supported categories
- stock movement and audit checks prevent invalid zero/negative entries where applicable

## 4. Indexes and constraints

Indexes and constraints are enforced as part of the EF Core model configuration. The implementation deliberately uses query-driven indexes for the measured reporting workloads and stock operations.

Examples from the code and migrations include:

- unique indexes on staff email and reversal stock movement patterns
- branch and stock-based query patterns for inventory and low-stock report workloads
- date- and branch-based indexes for daily sales and shipment searches

The performance evidence is documented in:

- `PERFORMANCE.md`
- `docs/performance-measurement.md`

## 5. Rowversion and concurrency

The Week 3 assignment requires rowversion or concurrency protection on the high-risk inventory and transaction tables. The solution includes concurrency-safe patterns and optimistic concurrency handling around inventory updates and transaction commits.

Implementation evidence:

- `src/ApexPost.Infrastructure/Persistence/SqlUnitOfWork.cs`
- `src/ApexPost.Infrastructure/Persistence/Repositories/SqlRepositories.cs`

The behaviors implemented are:

- `DbUpdateConcurrencyException` translated to a `ConcurrencyConflictException`
- explicit transaction boundaries for stock and reporting sequences
- checks to prevent stock from going negative during concurrent sales
- detection and safe handling of overlapping stock updates

## 6. Migrations

The solution uses EF Core Code First migrations.

Relevant migration files:

- `src/ApexPost.Infrastructure/Persistence/Migrations/20261031173410_InitialCreate.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261031173907_AddSqlReportingAndStockProcedures.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261031174229_SeedShippingZonesAndRates.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261031175835_GuardStockAdjustmentOverflow.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261101010000_UpdateDailyBranchSalesProcedure.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261102000000_AddReportFilters.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/20261103000000_HardenReportingProcedures.cs`

The idempotent deployment script is:

- `database/schema/ApexPostDb-idempotent.sql`

The migration flow is designed to be reviewed and re-applied cleanly rather than creating ad hoc schema drift.

## 7. Initial JSON import

The repository still includes a migration route from the original JSON-based world to the SQL system of record.

Key import implementation:

- `src/ApexPost.Infrastructure/Persistence/JsonDataImporter.cs`

The importer:

- verifies the source data directory
- creates a fingerprint of the import set
- prevents duplicate imports by fingerprint
- refuses import if the target database contains business data without a matching import record
- records the import in `ImportRuns`

This is the safe bridge from the legacy JSON dataset to the SQL-backed model.

## 8. Stored procedures

Stored procedures are created in the migration files and invoked via parameterized EF Core APIs rather than string concatenation.

Implemented procedures:

- `dbo.usp_DailyBranchSales`
- `dbo.usp_LowStockReport`
- `dbo.usp_AdjustStock`

These procedures provide the database boundary for operational reporting and stock adjustments, and they are used where the logic is better expressed as a server-side procedure rather than application-layer LINQ.

## 9. How to inspect the database using SSMS

Prerequisites:

- local SQL Server / LocalDB available on the machine
- connection string configured and working

Typical LocalDB connection example:

```text
Server=(localdb)\MSSQLLocalDB;Database=ApexPostDb;Integrated Security=True;TrustServerCertificate=True;
```

In SSMS:

1. Connect to `(localdb)\MSSQLLocalDB`
2. Expand Databases
3. Open `ApexPostDb`
4. Review tables and constraints
5. Inspect stored procedures under Programmability
6. Review query plans for the reporting workload
7. Use Object Explorer to inspect indexes and schema objects

For test isolation:

- temporary databases are named `ApexPost_SqlIT_*`
- persistent development database remains `ApexPostDb`

## 10. Database operations checklist

Recommended review flow for a local environment:

1. Confirm `ApexPostDb` is the active persistent dev database.
2. Confirm no `ApexPost_SqlIT_*` databases remain after tests.
3. Review EF Core migration history.
4. Inspect the procedure definitions under SQL Server.
5. Validate the business rows and reporting data are present.
6. Confirm audit and stock movement entries behave correctly.

## 11. Summary

This database design keeps the Week 2 business logic intact while creating a proper SQL Server data model with validation, reporting, and concurrency support. The schema is intentionally normalized, query-friendly, and operationally safe for the assignment workload.

## 12. Current data population audit

The persistent database was inspected without resetting or recreating it. Its import ledger contains a 430-row business snapshot with 200 transactions. The current `App_Data` preview is a different snapshot: 40 transactions, no `shipments.json` or `money-orders.json`, and 102 valid audit events. The full importer correctly refuses to merge a different business snapshot into a database that already contains business data.

The 102 existing audit events were therefore imported independently using the repeat-safe audit-log import. It validates the allowlisted audit fields, writes the rows and import ledger entry in one SQL transaction, and uses the source-file fingerprint to make an identical repeat a no-op. No App_Data file was modified. Current empty-table explanations:

| Table | Expected data source | Actual reason for initial emptiness | Required action | Verification |
|---|---|---|---|---|
| `AuditEntries` | Explicit historical import or the Web `AuditActionFilter` after an audited POST | No audited SQL-backed POST had run; the current App_Data audit file was not part of the existing 430-row SQL snapshot | Imported the 102 validated legacy events; new events are written by the SQL audit store | SQL count is 102; audit import repeat returns `Already imported` |
| `IdentityVerifications` | A money-order workflow when the policy requires and receives successful verification | No money orders existed in the SQL snapshot; no `money-orders.json` source exists | No seed rows; created only with a qualifying money order | SQL MVC and application SQL integration workflows verify the money-order relationship and masked value |
| `MoneyOrders` | The supported money-order counter-sale workflow | No `money-orders.json` source and no money-order workflow had run against the persistent database | No seed rows; create only through the supported business workflow | SQL integration workflow reads the created order and report row |
| `Shipments` | The supported shipment workflow, including shipping on a counter sale | No `shipments.json` source and no shipment workflow had run against the persistent database | No seed rows; create only through the supported business workflow | SQL integration workflow verifies shipment-to-transaction relationship |
| `StockMovements` | `usp_AdjustStock` for adjustments and stock restoration on a successful void/refund | Starting stock balances were imported, but no adjustment or transaction reversal had occurred in the persistent database | No opening-balance history is synthesized; movements are recorded by supported adjustment/reversal workflows | SQL integration tests verify adjustment and reversal rows |

These are operational/history tables, not reference-data seed tables. The persistent database was not given fabricated sales, shipments, money orders, verifications, or stock adjustments. The SQL integration workflows create representative records only in automatically cleaned `ApexPost_SqlIT_*` databases.

The current persistent minimum/reference-data counts are: 5 branches, 5 counters, 24 staff, 4 roles, 30 products, 3 product categories, 150 branch-stock rows, 10 shipping zones, 12 shipping rates, 200 transactions, 200 transaction lines, and 200 payments. Staff exceeds the Week 3 minimum of 20; the App_Data staff file has 20. The SQL transaction count exceeds the current App_Data file’s 40, so the two snapshots must not be treated as interchangeable.

## 13. Read-only SSMS verification queries

Connect to `(localdb)\MSSQLLocalDB`, select `ApexPostDb`, and run these read-only queries. The first query provides row counts for the required application tables:

```sql
SELECT 'Branches' AS TableName, COUNT_BIG(*) AS Rows FROM dbo.Branches
UNION ALL SELECT 'Counters', COUNT_BIG(*) FROM dbo.Counters
UNION ALL SELECT 'Staff', COUNT_BIG(*) FROM dbo.Staff
UNION ALL SELECT 'Roles', COUNT_BIG(*) FROM dbo.Roles
UNION ALL SELECT 'Products', COUNT_BIG(*) FROM dbo.Products
UNION ALL SELECT 'ProductCategories', COUNT_BIG(*) FROM dbo.ProductCategories
UNION ALL SELECT 'BranchStock', COUNT_BIG(*) FROM dbo.BranchStock
UNION ALL SELECT 'ShippingZones', COUNT_BIG(*) FROM dbo.ShippingZones
UNION ALL SELECT 'ShippingRates', COUNT_BIG(*) FROM dbo.ShippingRates
UNION ALL SELECT 'Shipments', COUNT_BIG(*) FROM dbo.Shipments
UNION ALL SELECT 'MoneyOrders', COUNT_BIG(*) FROM dbo.MoneyOrders
UNION ALL SELECT 'IdentityVerifications', COUNT_BIG(*) FROM dbo.IdentityVerifications
UNION ALL SELECT 'CounterTransactions', COUNT_BIG(*) FROM dbo.CounterTransactions
UNION ALL SELECT 'TransactionLines', COUNT_BIG(*) FROM dbo.TransactionLines
UNION ALL SELECT 'Payments', COUNT_BIG(*) FROM dbo.Payments
UNION ALL SELECT 'AuditEntries', COUNT_BIG(*) FROM dbo.AuditEntries
UNION ALL SELECT 'StockMovements', COUNT_BIG(*) FROM dbo.StockMovements
ORDER BY TableName;
```

Inspect stored workflow relationships without exposing unmasked identity data:

```sql
SELECT TOP (20)
    s.Id AS ShipmentId, s.CounterTransactionId, s.BranchCode, s.ZoneCode, s.ServiceLevel
FROM dbo.Shipments AS s
ORDER BY s.CreatedAtUtc DESC;

SELECT TOP (20)
    m.Id AS MoneyOrderId, m.TransactionId, m.BranchCode, m.Amount,
    v.Id AS IdentityVerificationId, v.DocumentType, v.MaskedDocumentNumber, v.IsVerified
FROM dbo.MoneyOrders AS m
LEFT JOIN dbo.IdentityVerifications AS v ON v.MoneyOrderId = m.Id
ORDER BY m.CreatedAtUtc DESC;

SELECT TOP (20)
    Id, BranchCode, Sku, QuantityDelta, QuantityBefore, QuantityAfter,
    MovementType, TransactionId, Reason, ChangedBy
FROM dbo.StockMovements
ORDER BY Id DESC;

SELECT TOP (20)
    Id, Timestamp, StaffId, Role, BranchCode, Controller, Action,
    StatusCode, DurationMilliseconds, CorrelationId, RouteValuesJson
FROM dbo.AuditEntries
ORDER BY Timestamp DESC;
```

Verify database selection and the migration/import ledger without printing a connection string:

```sql
SELECT DB_NAME() AS DatabaseName, @@SERVERNAME AS ServerName;
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT ImporterVersion, SourceCount, ImportedCount, SkippedCount
FROM dbo.JsonImportRuns
ORDER BY Id;
```

At the audit date, the post-import `ApexPostDb` counts for the five initially empty tables were `AuditEntries=102`, `IdentityVerifications=0`, `MoneyOrders=0`, `Shipments=0`, and `StockMovements=0`. The empty operational tables remain expected until their documented workflows run against the persistent database.
