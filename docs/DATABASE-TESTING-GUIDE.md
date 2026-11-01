# Database testing and access guide

## Engine and local connection

The Web application uses SQL Server through EF Core. Local development uses the LocalDB instance `(localdb)\MSSQLLocalDB` and Windows Integrated Security, so the signed-in Windows user is the database identity. There is no SQL username or password to share.

The normal local development catalog is `ApexPostDb`. Performance evidence used the separate retained catalog `ApexPost_PerfEvidence_20261101b`; it is fictional test data and is not the application catalog.

Check LocalDB from PowerShell:

```powershell
sqllocaldb info MSSQLLocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "SELECT @@SERVERNAME;"
```

In SSMS or Visual Studio SQL Server Object Explorer, use server name `(localdb)\MSSQLLocalDB`, choose **Windows Authentication**, and select `ApexPostDb`. Do not select SQL Server Authentication unless a separately managed SQL login has been created by an operator.

## Configure and migrate

Set the connection string in user-secrets, never in a tracked file:

```powershell
dotnet user-secrets set "ConnectionStrings:ApexPost" "Server=(localdb)\MSSQLLocalDB;Database=ApexPostDb;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False" --project src/ApexPost.Web
dotnet ef database update --project src/ApexPost.Infrastructure --startup-project src/ApexPost.Web
```

Use a different database name for experiments and test data. The Web project fails at startup with guidance when the connection string is absent. Do not use `App_Data` as a database source unless you intentionally run the documented import command.

To preview or import an existing JSON source without changing it:

```powershell
dotnet run --project tools/ApexPost.DemoSeed -- --import-preview --data-directory <read-only-json-directory>
dotnet run --project tools/ApexPost.DemoSeed -- --import-json --data-directory <read-only-json-directory>
```

Run the application after configuring user-secrets:

```powershell
dotnet run --project src/ApexPost.Web
```

The application does not seed accounts at startup. Do not use or publish credentials from `App_Data`. The four local accounts below are provisioned directly into SQL using the existing staff service and PBKDF2 hasher. The account tool is restricted to Development, Windows-authenticated LocalDB, and development/test catalog names. It verifies each account through the SQL-backed authentication service and never prints passwords or password hashes.

## Application Login Credentials for Integration Testing

The supported roles in the application are **Administrator**, **Branch Manager**, **Auditor**, and **Counter Clerk**. There is no Supervisor role. The following fictional accounts are local-development-only and are created in `dbo.Staff` and `dbo.StaffCredentials`, not in JSON. They use the first active branch and counter ordered by code/ID; in the current `ApexPostDb` this resolves to `AP001` / `COUNTER-AP001-1`.

| User ID / Username | Development Password | Role | Branch / Counter | Testing Purpose |
|---|---|---|---|---|
| `LOCALDEV-ADMIN` / `local-admin@example.test` | Local secret `DevelopmentTestAccounts:AdministratorPassword` | Administrator | `AP001` / `COUNTER-AP001-1` | Staff management, any branch's stock, cross-branch reports, and counter workflows. |
| `LOCALDEV-MANAGER` / `local-manager@example.test` | Local secret `DevelopmentTestAccounts:BranchManagerPassword` | Branch Manager | `AP001` / `COUNTER-AP001-1` | Branch-scoped reports, stock management, sales, voids, and refunds. |
| `LOCALDEV-AUDITOR` / `local-auditor@example.test` | Local secret `DevelopmentTestAccounts:AuditorPassword` | Auditor | `AP001` / `COUNTER-AP001-1` | Cross-branch reports and auditor service/money-order reporting; not stock management or counter sales. |
| `LOCALDEV-CLERK` / `local-clerk@example.test` | Local secret `DevelopmentTestAccounts:CounterClerkPassword` | Counter Clerk | `AP001` / `COUNTER-AP001-1` | Counter sales and same-branch inventory inquiry; not stock management or reports. |

The account names and roles above are the exact values provisioned by the tool. Passwords are random, unique per account, stored only in local .NET user-secrets, and stored as PBKDF2 hashes in SQL. The guide and repository do not contain their values.

The test branch is configured for 08:00–20:00 local time. Sales are permitted only during that window; a valid counter role can still receive a business-rule `403` outside opening hours. The isolated HTTP integration test uses an always-open synthetic branch so authorization can be checked independently of the clock. Administrator access to `/Staff` also requires the local `Security:AdminAllowedIps` allowlist; for local requests configure the loopback address that the server observes.

1. Configure the development SQL connection string as shown above and apply migrations. The provisioner does not create, migrate, reset, or delete databases.
2. Generate four local passwords and save them in the Web project's user-secrets store:

   ```powershell
   $passwordKeys = @(
       "AdministratorPassword",
       "BranchManagerPassword",
       "AuditorPassword",
       "CounterClerkPassword"
   )
   foreach ($key in $passwordKeys) {
       $password = [Convert]::ToBase64String(
           [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
       dotnet user-secrets set "DevelopmentTestAccounts:$key" $password --project src/ApexPost.Web
   }
   ```

3. Provision and verify the SQL accounts. The tool rejects non-Development environments, SQL authentication, other LocalDB instances, and catalogs other than `ApexPostDb` or `ApexPost_SqlIT_*`.

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   dotnet run --project tools/ApexPost.DemoSeed -- --provision-local-test-accounts
   ```

   It prints each username, role, branch, and counter after successful SQL-backed password verification. Re-running is safe. If a password secret was changed or lost, the tool rehashes the new password only for its exact `LOCALDEV-*` accounts after checking their email, role, branch, and active state. It refuses to modify any account that does not match those development-only identities; it never resets other staff credentials.

4. Retrieve only these local passwords when needed:

   ```powershell
   dotnet user-secrets list --project src/ApexPost.Web |
       Select-String '^DevelopmentTestAccounts:'
   ```

   This intentionally prints the selected secrets in the local terminal. Do not copy the output into documentation, tickets, recordings, or source control. To rotate a local test password, set a new random value for its user-secret and re-run the provision command; only the four exact local test credentials are rehashed.

5. Start the application with the same connection string and Development environment, open `/Account/Login`, and sign in with the username (email) and its local password. Verify the allowed/denied screens listed in the table; MVC authorization and branch scope are enforced server-side. To test `/Staff`, configure `Security:AdminAllowedIps` for the observed local loopback address as described in the README.

The separate synthetic JSON data generator also requires `DemoData:Password` in user-secrets (or `APEXPOST_DEMO_DATA_PASSWORD` in the current shell); it does not contain a built-in password. Its generated password hashes are only written to an explicitly selected fixture directory.

## Read-only verification queries

Run these against the intended catalog in SSMS, Visual Studio, or `sqlcmd -E`.

```sql
SELECT BranchCode, Name, Region, IsActive FROM dbo.Branches ORDER BY BranchCode;
SELECT Sku, Name, Category, UnitPrice, TaxCategory FROM dbo.Products ORDER BY Sku;
SELECT BranchCode, Sku, Quantity FROM dbo.BranchStock ORDER BY BranchCode, Sku;
SELECT TOP (50) Id, BranchCode, Status, OccurredAtUtc, Total FROM dbo.CounterTransactions ORDER BY OccurredAtUtc DESC;
SELECT TOP (50) TransactionId, Method, Amount, PaidAtUtc FROM dbo.Payments ORDER BY PaidAtUtc DESC;
SELECT TOP (50) Timestamp, StaffId, Controller, Action, StatusCode, CorrelationId FROM dbo.AuditEntries ORDER BY Timestamp DESC;
SELECT Id, Role, HomeBranchCode, IsActive FROM dbo.Staff WHERE Id LIKE 'LOCALDEV-%' ORDER BY Id;
SELECT Id, BranchCode, Name, IsActive FROM dbo.Counters ORDER BY BranchCode, Id;
SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT 'Branches' AS Entity, COUNT(*) AS Records FROM dbo.Branches
UNION ALL SELECT 'Products', COUNT(*) FROM dbo.Products
UNION ALL SELECT 'Transactions', COUNT(*) FROM dbo.CounterTransactions
UNION ALL SELECT 'AuditEntries', COUNT(*) FROM dbo.AuditEntries;
```

These statements are read only. Restrict any `INSERT`, `UPDATE`, stored-procedure adjustment, or cleanup work to a separate test catalog.

## Safe test execution

The SQL integration suite rejects unsafe catalog names. Create and retain an isolated `ApexPost_SqlIT_*` LocalDB catalog, then set its connection string only for the current shell:

```powershell
$env:APEXPOST_SQL_TEST_CONNECTION = "Server=(localdb)\MSSQLLocalDB;Database=ApexPost_SqlIT_<your-name>;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False"
dotnet restore ApexPost.sln
dotnet build ApexPost.sln --no-restore
dotnet test ApexPost.sln --no-restore
```

## Troubleshooting

| Symptom | Check |
|---|---|
| `LocalDB instance is not specified` or connection failure | Install SQL Server Express LocalDB, run `sqllocaldb start MSSQLLocalDB`, then retry with the exact instance name. |
| Web startup says connection string is required | Re-run the user-secrets command for `src/ApexPost.Web` and restart the application. |
| Login succeeds nowhere after a fresh database | Apply migrations, ensure branches/counters exist, then use the guarded `--provision-local-test-accounts` command above. The app does not create an implicit account. |
| Login succeeds but `/Transactions/Create` returns 403 | Confirm the account is a Counter Clerk, Branch Manager, or Administrator and that the branch is within its configured opening hours. |
| Administrator receives 403 at `/Staff` | Configure `Security:AdminAllowedIps` for the exact local client address; the staff-management IP restriction is separate from the Administrator role. |
| Migration fails because the database is in use | Stop local Web/test processes and rerun the explicit `dotnet ef database update` command. |
| Tests reject a database name | Use a separate catalog whose name begins `ApexPost_SqlIT_`; never point tests at `ApexPostDb`. |
