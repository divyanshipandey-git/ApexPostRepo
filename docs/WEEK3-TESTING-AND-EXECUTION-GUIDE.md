# Week 3 Testing and Execution Guide

## 1. Prerequisites

Before running the solution, ensure the following are installed and configured:

- .NET SDK compatible with the repository (the solution targets .NET 10)
- SQL Server LocalDB on the host machine
- an accessible LocalDB instance using `(localdb)\MSSQLLocalDB`
- permission to create and drop LocalDB databases for the integration tests

The application uses the SQL runtime connection string from either:

- `APEXPOST_SQL_CONNECTION`, or
- `ConnectionStrings:ApexPost` from user secrets

The test suite uses:

- `APEXPOST_SQL_TEST_CONNECTION`

## 2. Configure the SQL connection string

Use user secrets on the Web project for the persistent development database:

```powershell
dotnet user-secrets set "ConnectionStrings:ApexPost" "Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ApexPostDb;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True" --project src/ApexPost.Web
```

If the project is using a non-default LocalDB connection, use the actual value appropriate for the local environment. The runtime validates this through `SqlInfrastructureExtensions` and will throw if it is missing.

## 3. Configure the SQL test connection

Use an environment variable for the isolated integration test database:

```powershell
$env:APEXPOST_SQL_TEST_CONNECTION = 'Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ApexPost_SqlIT_Validation;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
```

The SQL integration tests require the connection string catalog to start with `ApexPost_SqlIT_` and will create a unique temporary database under that pattern during the fixture initialization.

## 4. Apply migrations

The existing EF Core migration structure is part of the repository:

```powershell
dotnet ef database update --project src/ApexPost.Infrastructure/ApexPost.Infrastructure.csproj --startup-project src/ApexPost.Web/ApexPost.Web.csproj
```

The generated idempotent deployment script is available in:

- `docs/sql/ApexPostDb-idempotent.sql`

## 5. Run the application

From the repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/ApexPost.Web/ApexPost.Web.csproj
```

The app starts with:

- MVC pages and controllers
- SQL runtime registration via `AddSqlInfrastructure`
- authentication and authorization policies
- health endpoint at `/health`

## 6. Verify SQL connectivity and SQL-only runtime

The local development user-secret is configured for `(localdb)\MSSQLLocalDB` and `ApexPostDb`. Avoid printing or copying the secret value. Verify the selected database directly:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d ApexPostDb -Q "SELECT DB_NAME() AS DatabaseName, @@SERVERNAME AS ServerName;"
```

Verify EF Core report reads through the configured SQL connection:

```powershell
dotnet run --project tools/ApexPost.DemoSeed -- --sql-report-smoke
```

The verified persistent database returned 200 transactions and 150 inventory rows. The Web composition root registers `AddSqlInfrastructure`; no JSON repository or JSON persistence options are registered there. JSON file access is confined to the explicitly invoked importer/legacy JSON infrastructure. `SqlRuntimeRegistrationTests` asserts the SQL repositories are resolved and the JSON registrations are absent. `SqlWebApplicationIntegrationTests` exercises MVC reads and writes against an isolated SQL database.

The SQL-unavailable probe was also exercised through the Web login POST: it returned HTTP 500 Problem Details with a trace/correlation ID, not a JSON-backed login result. The actual `App_Data` directory was not renamed or deleted. SQL-only behavior was established by dependency-registration assertions, SQL-backed MVC tests, and operation with a test-only nonexistent JSON data path.

To import the existing historical audit JSONL into an already-populated SQL database without merging the mismatched business snapshot, use the controlled audit-only importer:

```powershell
dotnet run --project tools/ApexPost.DemoSeed -- --import-audit-jsonl --data-directory App_Data
```

It validates the audit allowlist, commits the entries and import ledger row together, and returns a no-op on an identical repeat. Do not use the full JSON import on the populated `ApexPostDb`: its current snapshot differs from the original SQL import and the full importer is designed to reject that merge.

## 7. Run the individual test projects

### Domain tests

```powershell
dotnet test tests/ApexPost.Domain.Tests/ApexPost.Domain.Tests.csproj
```

### Application tests

```powershell
dotnet test tests/ApexPost.Application.Tests/ApexPost.Application.Tests.csproj
```

### Web integration tests

```powershell
dotnet test tests/ApexPost.Web.IntegrationTests/ApexPost.Web.IntegrationTests.csproj
```

## 8. Run the complete solution test suite

```powershell
dotnet test ApexPost.sln --logger "console;verbosity=minimal"
```

With `APEXPOST_SQL_TEST_CONNECTION` configured, the validator result is:

- Passed: 390
- Failed: 0
- Skipped: 0
- Total: 390

Project totals: 112 Domain tests, 201 Application tests, and 77 Web integration tests. The SQL-backed Application and Web tests were enabled by the isolated LocalDB connection.

The SQL coverage includes repository reads/writes, transaction rollback, audit persistence and repeat-safe import, inventory adjustment/reversal movements, shipment and money-order/identity persistence, and SQL-backed MVC reads/writes. The SQL-backed sale and reversal assertions re-read data through new contexts after the operation.

## 9. Check test results and database cleanup

After a successful SQL-backed run:

- the isolated temp databases should be removed automatically by the fixtures
- no `ApexPost_SqlIT_*` databases should remain in LocalDB
- `ApexPostDb` should remain as the persistent development database

To verify manually:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "SELECT name FROM sys.databases WHERE name LIKE 'ApexPost%' ORDER BY name;"
```

Expected result:

- `ApexPostDb`
- no temporary `ApexPost_SqlIT_*` databases left behind

## 10. Troubleshooting common errors

### Missing connection string

Symptoms:

- invalid operation exception when the application starts

Fix:

- set `ConnectionStrings:ApexPost` in user-secrets or set `APEXPOST_SQL_CONNECTION`

### SQL integration tests skipped

Symptoms:

- zero SQL tests run because `APEXPOST_SQL_TEST_CONNECTION` is not set

Fix:

- set the environment variable before running the test suite

### LocalDB unavailable

Symptoms:

- connection failure or LocalDB instance missing

Fix:

- ensure SQL Server LocalDB is installed and reachable on the host

### Temporary database left behind

Symptoms:

- `ApexPost_SqlIT_*` remains after test execution

Fix:

- inspect the test fixture cleanup logic in:
  - `tests/ApexPost.Application.Tests/Persistence/SqlServerIntegrationTests.cs`
  - `tests/ApexPost.Web.IntegrationTests/SqlWebApplicationIntegrationTests.cs`

## 11. Final validation checklist

- Configuration set for `ApexPostDb`
- `ApexPOST_SQL_CONNECTION` or user-secrets value present
- `APEXPOST_SQL_TEST_CONNECTION` set for SQL integration suite
- migrations applied or available in source control
- full suite run successfully
- temporary databases cleaned up
- persistent database preserved as `ApexPostDb`
- SQL-only repository registration and failure behavior verified
- historical audit import is repeat-safe; operational history tables remain empty until their workflows run

This is the expected final Week 3 validation state for the repository.
