# ApexPost Counter Platform

ASP.NET Core MVC counter platform for ApexPost Logistics. The Week 3 implementation preserves the Week 2 business workflows and uses SQL Server as the Web application's persistent runtime data source.

## Prerequisites

- .NET 10 SDK
- Visual Studio 2026 or a compatible editor
- Git

## Build and test

```powershell
dotnet --info
dotnet restore ApexPost.sln
dotnet build ApexPost.sln --no-restore
dotnet test ApexPost.sln --no-restore
```

The SQL Server integration tests are opt-in. Set `APEXPOST_SQL_TEST_CONNECTION` to a LocalDB connection string with a catalog prefix of `ApexPost_SqlIT_` to include them. The fixtures create uniquely named temporary databases, apply migrations, and clean them up automatically. Do not point the variable at `ApexPostDb` or another persistent database.

```powershell
$env:APEXPOST_SQL_TEST_CONNECTION = "Server=(localdb)\MSSQLLocalDB;Database=ApexPost_SqlIT_Local;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False"
dotnet test ApexPost.sln --no-restore
```


Run the web application:

```powershell
dotnet run --project src/ApexPost.Web
```

The Web application uses SQL Server as its only runtime business-data source.

## Running the tests

One command, no manual setup (no SQL Server, no network, no secrets needed):

```powershell
dotnet test ApexPost.sln
```

In Visual Studio / VS Code every test shows up in **Test Explorer** (xUnit). To run one project: `dotnet test tests/ApexPost.UnitTests`; one class or test: `dotnet test --filter "FullyQualifiedName~HeavyParcelSurchargeTests"`.

### Last full run

| Project | Level | Passed | Skipped | Failed |
|---|---|---:|---:|---:|
| `ApexPost.UnitTests` (new) | unit, Moq, fake `HttpMessageHandler`, TDD | 146 | 0 | 0 |
| `ApexPost.IntegrationTests` (new) | SQLite repositories, API through `WebApplicationFactory` | 39 | 0 | 0 |
| `ApexPost.Specs` (new) | Reqnroll BDD: 5 features, 25 API scenarios, 12 service scenarios (incl. the Copilot-drafted feature) | 37 | 0 | 0 |
| `ApexPost.Domain.Tests` | unit | 330 | 0 | 0 |
| `ApexPost.Application.Tests` | unit + file persistence + opt-in SQL Server | 168 | 22 | 0 |
| `ApexPost.Api.Tests` | unit | 21 | 0 | 0 |
| `ApexPost.Courier.Offline.Tests` | real Couchbase Lite on a temp folder | 53 | 0 | 0 |
| `ApexPost.Web.IntegrationTests` | MVC pipeline + opt-in SQL Server | 76 | 5 | 0 |
| **Total** | | **870** | **27** | **0** |

The 27 skipped tests are the opt-in SQL Server tests (they need `APEXPOST_SQL_TEST_CONNECTION`, see above). With LocalDB enabled the whole solution gives **893 passed, 0 skipped, 0 failed** ([docs/evidence/test-run-with-sqlserver.txt](docs/evidence/test-run-with-sqlserver.txt)); the default run is [docs/evidence/test-run.txt](docs/evidence/test-run.txt).

### Code coverage (evidence, not a target)

```powershell
dotnet test ApexPost.sln --collect:"XPlat Code Coverage" --results-directory cov
```

`coverlet.collector` is referenced by the test projects. Line coverage of the production assemblies (generated code, EF migrations and the NSwag client excluded, results of all test projects merged) is in [docs/evidence/coverage-summary.md](docs/evidence/coverage-summary.md); read it together with section 12 of [TESTING-STRATEGY.md](TESTING-STRATEGY.md), which says where the suite is blind.

### Continuous integration and branches

* `.github/workflows/tests.yml` runs `dotnet test ApexPost.sln` on every push and pull request (Windows runner, no SQL Server, no secrets).
* The default branch of this repository is **`master`** (the lab text says `main`). `week5-testing` was merged into it by pull request #12, and the completion work by #13; the `RED`/`GREEN`/`REFACTOR` commits are visible with `git log --grep="^RED\|^GREEN\|^REFACTOR"`.
* A screenshot of the run is not stored in the repository; the raw runner output is (`docs/evidence/test-run*.txt`), and Test Explorer in Visual Studio shows the same tests.

### Test documents

* [TESTING-STRATEGY.md](TESTING-STRATEGY.md): risk map, pyramid, framework choice, review of every existing test class, data access strategy, what is not tested, reflection.
* [docs/TESTING-DIAGRAMS.md](docs/TESTING-DIAGRAMS.md): diagrams of the strategy, TDD cycle, BDD flow and data-access choices (picture: [docs/testing-strategy-diagram.svg](docs/testing-strategy-diagram.svg)).
* [TDD-LOG.md](TDD-LOG.md): the heavy parcel surcharge built test-first. `git log --grep="^RED\|^GREEN\|^REFACTOR"` shows the cycle.
* [COPILOT-LOG.md](COPILOT-LOG.md): AI-assisted drafts, review decisions, and the three deliberate-breakage proofs (`docs/evidence/`).
* [VIVA-NOTES.md](VIVA-NOTES.md): short, simple notes for the 5-minute viva.

Notes: the API DTO rule for `PATCH .../status` is stricter than the domain rule (a `reason` is required for `Delivered` as well as `Failed`); the integration tests use a fake, test-only JWT signing key that exists only inside the test process.

## API Documentation: OpenAPI vs Swagger UI

`src/ApexPost.Api` is an `[ApiController]` Web API (attribute routing, a `tracking` route constraint, DataAnnotations as the only validation source). It produces its API description with **one generator only: the built-in `Microsoft.AspNetCore.OpenApi`**. Swashbuckle's generator, `NSwag.AspNetCore` and the old `Microsoft.AspNetCore.Mvc.Versioning` package are not used.

### OpenAPI documents (built-in .NET)
- **Endpoints**: `/openapi/v1.json` and `/openapi/v2.json` (one document per API version, named after the Asp.Versioning ApiExplorer group `v1` / `v2`).
- **Registered by**: `AddOpenApi("v1")` and `AddOpenApi("v2")` + `MapOpenApi()` in `Program.cs`; document/operation transformers live in `src/ApexPost.Api/OpenApi/ApiDocuments.cs` (title, version taken from the group, JWT bearer scheme, API key scheme, deprecation flags).
- **Purpose**: machine-readable contract for code generation and CI.
- **v1 is deprecated** (`[ApiVersion("1.0", Deprecated = true)]`): every v1 operation is marked `deprecated: true`, v1 responses carry `Deprecation: true` and a `Sunset` header (2027-07-01), and every response reports `api-supported-versions` / `api-deprecated-versions`.
- **v2 returns different data**: the v2 actions call the v2 service methods and return the nested `recipient` and `latestScan` shape; v1 returns the flat shape.

### Swagger UI (viewer only)
- **Endpoint**: `/swagger` (Development/Staging only), from `Swashbuckle.AspNetCore.SwaggerUI`.
- Swagger UI does **not** generate anything; its version dropdown points at `/openapi/v1.json` and `/openapi/v2.json`.

| Aspect | OpenAPI documents | Swagger UI |
|--------|-------------------|------------|
| **Audience** | Machines (code gen, CI/CD) | Humans (developers, QA) |
| **Source** | Built-in `Microsoft.AspNetCore.OpenApi` | Reads the OpenAPI documents |
| **Availability** | Development/Staging | Development/Staging only |

### Contracts and the client SDK
The committed `contracts/v1.json` and `contracts/v2.json` must be **exported from the running API**, not edited by hand:

```powershell
dotnet run --project src/ApexPost.Api --launch-profile https   # terminal 1 (secrets configured, see below)
./scripts/Export-Contracts.ps1                                   # terminal 2: writes contracts/v1.json + v2.json
./scripts/Generate-Client.ps1                                    # NSwag: contracts/v2.json -> src/ApexPost.Client/ApexPost.Client.cs
```

(`scripts/export-contracts.sh` and `scripts/generate-client.sh` do the same on bash.) The generated `src/ApexPost.Client/ApexPost.Client.cs` is committed and compiled into `ApexPost.Client`; `tools/ApexPost.Client.Consumer` makes real HTTP calls through that client.

> **Contracts.** `contracts/v1.json` and `contracts/v2.json` were exported from the running API and the client was regenerated from `v2.json`. Re-run the export and generate scripts after every API change (the PowerShell script needs PowerShell 7; `curl` against `/openapi/{v}.json` is equivalent).

### API configuration and secrets (nothing secret is committed)
`appsettings.json` contains no connection string, signing key or API key. Configure them with user-secrets:

```powershell
cd src/ApexPost.Api
dotnet user-secrets set "ConnectionStrings:ApexPost" "Server=(localdb)\MSSQLLocalDB;Database=ApexPostApi;Trusted_Connection=True;Encrypt=False"
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random characters>"
dotnet user-secrets set "PartnerApiKey" "<random partner key>"   # or one key per partner: "PartnerApiKeys:AcmeCo"
```

Environment variables work too (`Jwt__SigningKey`, `ConnectionStrings__ApexPost`, `PartnerApiKey`). The API refuses to start without `Jwt:SigningKey`. In Development, `/dev/token` only answers local requests and only for the ids listed under `DevTokens:CourierIds` / `DevTokens:AdminIds` in `appsettings.Development.json`.

See [VERSIONING.md](VERSIONING.md) for the versioning strategy, [API-NOTES.md](API-NOTES.md) for the endpoint reference, [SECURITY.md](SECURITY.md), [RESILIENCE.md](RESILIENCE.md) and [OFFLINE-DESIGN.md](OFFLINE-DESIGN.md).

The Web application uses SQL Server as its only runtime business-data source. JSON repositories remain for isolated legacy tests and for the explicit read-only preview/initial-import workflow; Web startup does not register them and does not fall back to JSON if SQL Server is unavailable. No production data is included in source control.

## SQL Server development setup

Install .NET 10 and SQL Server LocalDB (or provide a SQL Server connection string). Configure the Development connection string in user-secrets; the application fails at startup with a setup message if it is missing:

```powershell
dotnet user-secrets set "ConnectionStrings:ApexPost" "Server=(localdb)\MSSQLLocalDB;Database=ApexPostDb;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False" --project src/ApexPost.Web
```

Create/update the database explicitly. The Web application does not run migrations on startup and does not call `EnsureCreated`:

```powershell
dotnet ef database update --project src/ApexPost.Infrastructure --startup-project src/ApexPost.Web
```

The generated idempotent deployment script is [database/schema/ApexPostDb-idempotent.sql](database/schema/ApexPostDb-idempotent.sql). The [database guide](database/README.md) explains SQL file ownership, execution order, and verification. To run the Web app after migrations:

```powershell
dotnet run --project src/ApexPost.Web
```

The root [SQL guide](sql/README.md) indexes the canonical reporting queries and procedures and provides transaction and blocking demonstrations. Scripts with SQLCMD `:r` directives must be run in SQLCMD mode from the repository root.

### Week 3 verification index

- [Complete Week 3 documentation](docs/WEEK3-COMPLETE-DOCUMENTATION.md)
- [Current SQL/application ADRs](docs/adr/README.md)
- [SQLCMD scripts and least-privilege guidance](sql/README.md)

Historical Week 3 gap analyses and process records are retained under `docs/`; the links above identify the current implementation and verification entry points.

Provision the four fictional, SQL-backed local role accounts with the guarded tool documented in the [database testing guide](database/README.md). Passwords are generated into user-secrets and are never printed by the tool or stored in source control.

Check database rows using SQL Server Object Explorer or `sqlcmd`, for example:

```sql
SELECT COUNT(*) AS BranchCount FROM dbo.Branches;
SELECT COUNT(*) AS StaffCount FROM dbo.Staff;
SELECT COUNT(*) AS ProductCount FROM dbo.Products;
SELECT COUNT(*) AS TransactionCount FROM dbo.CounterTransactions;
```

### JSON source preview and import

Keep the original JSON directory unchanged. Preview it before importing, then use the separate import command after migrations. The importer is read-only against its source and repeatable for the same source fingerprint:

```powershell
dotnet run --project tools/ApexPost.DemoSeed -- --import-preview --data-directory C:\Path\To\JsonSource
dotnet run --project tools/ApexPost.DemoSeed -- --import-json --data-directory C:\Path\To\JsonSource
```

The full import only accepts a source snapshot when the target database is empty or the same fingerprint was already imported. If an existing SQL database contains a different business snapshot, do not rerun or merge the full import. The existing historical audit file can be imported separately and repeat-safely without changing business rows:

```powershell
dotnet run --project tools/ApexPost.DemoSeed -- --import-audit-jsonl --data-directory App_Data
```

This command imports only validated entries from `audit.jsonl`, writes an import-ledger record, and does not run during Web startup. The four initially empty operational tables—`IdentityVerifications`, `MoneyOrders`, `Shipments`, and `StockMovements`—are populated by their supported workflows, not artificial seed records. See the [Week 3 complete documentation](docs/WEEK3-COMPLETE-DOCUMENTATION.md) for the latest observed counts and read-only SSMS verification queries.

For isolated synthetic testing only, the tool can create a separate JSON source with 5 branches, 20 staff, 30 products and 200 explicitly synthetic transactions using `--demo-json-200 --data-directory <new-empty-directory>`. Configure `DemoData:Password` through user-secrets (or `APEXPOST_DEMO_DATA_PASSWORD` for the current shell); the generator has no built-in password. These records are test/demo data, never historical production evidence. The importer does not edit source files or import automatically at Web startup.

## Configuration and secrets

Never commit API keys or passwords. Configure development-only secrets with user-secrets after initializing the Web project:

```powershell
dotnet user-secrets set "Integration:ApiKey" "<local-development-key>" --project src/ApexPost.Web
dotnet user-secrets set "Integration:AllowedOrigin" "https://<approved-courier-widget-origin>" --project src/ApexPost.Web
```

The Web project already has a `UserSecretsId`; `dotnet user-secrets init` is only needed if that project setting is removed.

Configure `Integration:AllowedIps` as an array in environment configuration for the courier partner's observed client addresses. The API key is required for `POST /api/integrations/courier/shipments/{shipmentId}/status`; the request JSON is `{ "status": "In transit" }`. A successful response contains `shipmentId`, `status`, and `receivedAtUtc`. The route also enforces the configured IP allowlist and partner rate limit. CORS allows the configured browser origin; it does not authenticate callers. See [DESIGN.md](DESIGN.md) for reverse-proxy/IP trust details.

The administrator staff-management page also requires an IP allowlist. Configure `Security:AdminAllowedIps` as an array for trusted administrator client IPs. For local PowerShell development, for example:

```powershell
$env:Security__AdminAllowedIps__0 = "127.0.0.1"
```

Sample courier status request (replace the shipment ID and provide the configured secret through the environment):

```powershell
$headers = @{ "X-Api-Key" = $env:APEXPOST_INTEGRATION_API_KEY; "Content-Type" = "application/json" }
Invoke-RestMethod -Method Post -Uri "https://localhost:5001/api/integrations/courier/shipments/SHP-EXAMPLE/status" -Headers $headers -Body '{"status":"In transit"}'
```

The staff MVC inventory inquiry is at `/Inventory/Index`; catalogue details are at `/Products`. Administrators can set a fixed unit price for a product category at `/Products/PriceRevision`; this is the current assumed price-revision business rule. Branch Managers and Administrators can adjust SQL-backed stock at `/Stock`. Staff administration is at `/Staff` and requires `Security:AdminAllowedIps`. Counter sales are created at `/Transactions/Create`; shipment, money-order, identity, transaction, payment, inventory, reporting, and audit persistence use SQL Server repositories in the running Web application. Voids and full refunds require an authorized manager/administrator and a reason; eligible retail stock is restored in the same database transaction with a unique reversal movement to prevent duplicate restoration. These workflows do not cancel shipments or money orders and do not call a payment provider. Shipping rates and tax values in `appsettings.json` are illustrative configuration and must be replaced with approved values before operational use. Successful sales use receipt redirects; receipt shipment links use the shipment service-line reference. The dashboard is `/`, reports are at `/Reports`, and transaction review is at `/Transactions`. Courier tracking and notifications remain simulations. The product cache is scoped to the SQL-backed repository lifetime.

### Daily sales and reports

Use an authorized Branch Manager, Auditor, or Administrator account and open `/Reports`. Select a UTC report date, select a branch (Auditors and Administrators can choose any branch or all branches; Branch Managers remain restricted to their home branch), and enter a non-negative low-stock threshold from 0 to 100000. The screen shows retail, services, and overall daily sales totals, low-stock SKUs and quantities, shipping counts grouped by zone/service, and (for Auditors/Administrators) branch-level money-order and verification counts. Empty results show an explicit no-records message; reports do not generate synthetic activity. Procedure/query invocation examples are under [database/queries](database/queries/).

The remediation intentionally excludes Couchbase, `ApexPost.CounterTerminal`, offline-first behavior, synchronization, SyncInbox/outbox, and conflict-resolution workflows. See the [Week 3 complete documentation](docs/WEEK3-COMPLETE-DOCUMENTATION.md) for implemented status and remaining evidence gaps.

## Architecture

- `ApexPost.Domain`: business entities, value objects, aggregates, domain rules and domain events. No framework or infrastructure references.
- `ApexPost.Application`: use cases, DTOs, repository/service abstractions and orchestration. References Domain.
- `ApexPost.Infrastructure`: SQL Server repositories, EF Core persistence and the explicit JSON import/legacy test adapters. Implements Application abstractions.
- `ApexPost.Web`: MVC controllers, Razor views, authentication, middleware and filters. Composition root references Application and Infrastructure.
- `tests/`: domain/application unit tests and web integration tests.

See [DESIGN.md](DESIGN.md) and [docs/adr](docs/adr/).

The SQL schema, migrations, import boundary, persistence status, and remaining performance limitations are recorded in [DESIGN.md](DESIGN.md) and [docs/WEEK3-COMPLETE-DOCUMENTATION.md](docs/WEEK3-COMPLETE-DOCUMENTATION.md). See the [database testing and access guide](database/README.md) for LocalDB connection, migration, query, and safe test instructions.

## Git workflow

Create short-lived feature branches from `main`, make small commits, run build/tests, and open a pull request. Merge only reviewed, passing work. Keep secrets and local runtime data out of source control.

## Status

The current implementation includes branch dashboards and repository-backed reports, staff administration, product catalogue and stock adjustments, shipping-label preview, staff cookie authentication, courier parcel-status integration, mixed counter sales, transaction-linked money orders and identity checks, authorized void/full-refund workflows, cached reflection validation, the C# 14 extension-member/`field` examples, and a `ValueTask` product cache. The label preview is not carrier-ready because shipments do not yet capture a recipient postal address; courier tracking and payment settlement remain simulations. See [docs/RUBRIC-CHECKLIST.md](docs/RUBRIC-CHECKLIST.md) for requirement-level evidence and remaining operational gaps.

### Courier client and OpenAPI version
- Courier domain model and repository port: `src/ApexPost.Courier.Domain`; Couchbase Lite implementation: `src/ApexPost.Courier.Offline`.
- The courier client is the .NET console app `src/ApexPost.Courier.Console` (stand-in for the MAUI app; see [SECURITY.md](SECURITY.md)). The Part A harness is `tools/ApexPost.Courier.Offline.Console`.
- The built-in generator is configured to emit **OpenAPI 3.0** (`options.OpenApiVersion`) so NSwag can generate the client; `nswag run nswag.json` regenerates `src/ApexPost.Client` from `contracts/v2.json`, and `scripts/Export-Contracts.ps1` refreshes `contracts/` from the running API.
