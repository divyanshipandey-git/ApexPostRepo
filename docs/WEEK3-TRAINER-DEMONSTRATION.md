# Week 3 Trainer Demonstration

This demonstration sequence is designed for trainer review and is based on the current implementation in the repository. It uses the operational flows that are actually present in the application and the database schema that is currently implemented.

## 1. Application startup

1. Validate the development connection string is configured for `ApexPostDb`.
2. Start the app:

```powershell
dotnet run --project src/ApexPost.Web/ApexPost.Web.csproj
```

3. Confirm the app starts successfully and the SQL runtime is active.
4. Open `/health` and confirm the health endpoint responds successfully.

Expected evidence:

- `Program.cs` wires MVC and SQL infrastructure together.
- `SqlInfrastructureExtensions` requires the `ApexPost` connection string.
- `ApexPostDbContext` connects to SQL Server through EF Core.

## 2. Database connection verification

Use the configured LocalDB connection string to confirm the persistent database exists:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "SELECT name FROM sys.databases WHERE name = 'ApexPostDb';"
```

Expected result:

- `ApexPostDb` is present
- no temporary SQL test databases are active unless the test fixtures are currently running

## 3. Login demonstration

Use the development-only test accounts provisioned by the demo-seed tool or by the app’s local setup flow.

Typical account setup is connected to the user-secrets configuration and LocalDB environment. The repository includes the account-provisioning flow in the DemoSeed tool.

Validation steps:

1. Start the app in Development.
2. Navigate to the login screen.
3. Sign in with an existing staff account.
4. Confirm the cookie authentication flow, role claims, and branch claim assignment succeed.

## 4. Business workflow demonstration

### Product and inventory inspection

- Navigate to product catalogue and inventory views.
- Confirm catalogue and branch stock are read from SQL-backed repositories.
- Explain that imported opening balances belong in `BranchStock`; they do not imply historical rows in `StockMovements`.

### Counter transaction creation

- Create a mixed transaction with both retail and service items.
- Confirm that stock is decremented and payment is recorded.
- Confirm that transaction, lines, payment, and tax rows are persisted via the SQL model.
- The SQL integration workflow also creates a shipment plus a high-value money order with successful masked identity verification; verify the foreign-key links in the isolated integration database.

Expected evidence in the database:

- `CounterTransactions`
- `TransactionLines`
- `TransactionTaxRates`
- `Payments`
- `Shipments`
- `MoneyOrders`
- `IdentityVerifications`

### Shipping and money-order workflow

- Create a shipment based on the configured shipping profile.
- Submit a money order with identity verification.
- Validate that masked identity data is stored rather than the raw document number.

The persistent development database currently has no shipment or money-order source records, so these rows are created in the SQL integration workflow, not fabricated as persistent seed data. The App_Data directory has no `shipments.json` or `money-orders.json`.

### Stock adjustment and reversal

- Use the manager stock-adjustment workflow to execute `usp_AdjustStock` and inspect the resulting movement row in the SQL integration database.
- Perform a void/refund through the supported manager flow and verify stock restoration plus the `ReversalRestore` movement in the SQL integration database.

### Historical and new audit records

- The existing App_Data `audit.jsonl` contains 102 validated events. It was imported into `ApexPostDb` using the repeat-safe audit-only importer because the existing SQL business import is a different snapshot.
- New audited POST actions are written by the SQL `AuditActionFilter` store.
- The current persistent database has `AuditEntries=102`; manager reversal requests in the integration workflow also verify audit persistence.

For read-only row-count and relationship queries, use [WEEK3-DATABASE-GUIDE.md](./WEEK3-DATABASE-GUIDE.md#13-read-only-ssms-verification-queries).

## 5. Reports and stored procedures

Use the built-in reporting screens to demonstrate:

- daily branch sales
- low-stock report
- shipment volume by zone/service
- money-order or identity-verification auditor view

Then inspect the database objects directly in SSMS or SQL execution to confirm the stored procedures are present and callable.

## 6. Transaction rollback or concurrency demonstration

A practical demonstration can include one of the following:

- generating a failed stock adjustment path and confirming the database does not accept negative stock
- attempting a concurrent stock reduction scenario to confirm conflict handling
- validating that the application surfaces a clear concurrency error rather than allowing data corruption

The implementation is designed to highlight these conflict cases and preserve correctness.

## 7. Performance evidence demonstration

Use the repository evidence in:

- `PERFORMANCE.md`
- `docs/performance-measurement.md`

To explain:

- actual workload measurement setup
- SQL plan evidence and captured timings
- findings for low-stock, daily sales, shipment, and money-order query workloads
- why no broad production claims are made from LocalDB measurements

## 8. Test execution demonstration

Run the complete suite using the SQL test connection:

```powershell
$env:APEXPOST_SQL_TEST_CONNECTION = 'Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ApexPost_SqlIT_Validation;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
dotnet test ApexPost.sln --logger "console;verbosity=minimal"
```

Expected result:

- 379 passed
- 0 failed
- 0 skipped
- 379 total

Then confirm the temporary databases were cleaned up after the run.

## 9. Final demonstration closing

At the end of the demonstration, the trainer should verify:

- `ApexPostDb` remains the persistent development database
- no temporary `ApexPost_SqlIT_*` databases remain after the suite
- the SQL-backed runtime is functioning under a LocalDB-backed SQL Server environment
- user-secrets and SQL test configuration are in place
- Week 2 business rules are preserved while the system uses relational persistence
- workflow-driven tables are empty until their operations execute against `ApexPostDb`; test-created workflow rows are isolated and cleaned up

This is the expected final proof state for the Week 3 submission.
