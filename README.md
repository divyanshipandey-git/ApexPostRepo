# ApexPost Counter Platform

ASP.NET Core MVC counter platform for ApexPost Logistics. This repository is the Week 2 replacement for the Week 1 TechNova RMS console application.

## Prerequisites

- .NET 10 SDK
- Visual Studio 2026 or a compatible editor
- Git

## Build and test

```powershell
dotnet --info
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Run the web application:

```powershell
dotnet run --project src/ApexPost.Web
```

The application uses JSON files in its configured data directory. It does not ship production data. A development-only console tool can provision synthetic demo records in an explicitly chosen empty directory.

By default, JSON files are stored in `<solution-root>\App_Data` (for this checkout: `C:\DotNetTraining\Day1\ApexPost.CounterPlatform\App_Data`), outside `bin` and `obj`. The solution root is discovered from the running assembly location, so Web, test, and seed-tool processes do not depend on the shell's current directory. In Visual Studio, open the solution folder and browse to `App_Data`; JSON files appear there as the application writes them. Missing files are treated as empty collections and are created on the first write. The directory and runtime logs are ignored by Git, and Web static-file serving is limited to `wwwroot`.

`JsonPersistence:DataDirectory` remains an explicit override. Use the same directory for the seed tool and Web application. For a new local demo dataset, inspect the target first. If `App_Data` does not exist, create it; if it already exists, inspect and back it up before proceeding. Run from the solution root:

```powershell
if (-not (Test-Path .\App_Data)) { New-Item -ItemType Directory -Path .\App_Data }
Get-ChildItem .\App_Data -Force
# Continue only when this is a new, empty directory or the seed tool can verify its unchanged manifest.
dotnet run --project tools/ApexPost.DemoSeed -- --data-directory App_Data
dotnet run --project src/ApexPost.Web
```

Both commands now resolve relative `App_Data` to the solution root, which is also the Web default. If using a different directory, supply that same absolute path to both sides:

```powershell
dotnet run --project tools/ApexPost.DemoSeed -- --data-directory C:\ApexPostData
dotnet user-secrets set "JsonPersistence:DataDirectory" "C:\ApexPostData" --project src/ApexPost.Web
```

You can inspect or change the Web override in Visual Studio's project User Secrets, or set the equivalent environment variable `JsonPersistence__DataDirectory`. If you previously used the old development output path, select it explicitly with User Secrets, for example `C:\DotNetTraining\Day1\ApexPost.CounterPlatform\src\ApexPost.Web\bin\Debug\net10.0\App_Data`. Existing files are never moved, merged, replaced, or deleted automatically. Before changing directories, back up both locations and decide which dataset is authoritative; do not seed a non-empty directory unless the tool verifies its existing seed manifest.

### Provision isolated demo data

`App_Data` is the default target. The seed tool refuses a non-empty directory unless it verifies that directory's own seed manifest, so inspect/backup existing data first. To seed in a separate location, use the same path for the tool and Web:

```powershell
New-Item -ItemType Directory -Path C:\ApexPostDemoData
dotnet run --project tools/ApexPost.DemoSeed -- --data-directory C:\ApexPostDemoData
dotnet user-secrets set "JsonPersistence:DataDirectory" "C:\ApexPostDemoData" --project src/ApexPost.Web
dotnet run --project src/ApexPost.Web
```

The tool provisions 5 synthetic branches, 20 synthetic staff accounts across all four roles, 30 products, branch stock, and 40 paid sample transactions. `src/ApexPost.Web/appsettings.json` supplies 10 destination zones (12 zone/service-level pricing rules total). The transaction entity currently has no occurrence timestamp, so the 40 sample transactions are useful for listings and workflow testing but cannot be treated as date-accurate history for daily reports.

The login page is for provisioned staff only. Public self-registration and password recovery are not implemented. Administrator-only staff management (add, update, deactivate) remains an assignment requirement and is not yet available; do not use a public account-creation flow.

Demo sign-in uses the following synthetic accounts. These are local training credentials only; change them before any shared or deployed use. All demo accounts use the password `ApexPost-Demo-Only-2026!`.

| Role | Demo email | Branch |
| --- | --- | --- |
| Counter Clerk | `counterclerk01@example.test` | AP001 |
| Branch Manager | `branchmanager02@example.test` | AP002 |
| Auditor | `auditor03@example.test` | AP003 |
| Administrator | `administrator04@example.test` | AP004 |

The seed command refuses a non-empty directory unless its manifest exists and every seeded file still matches the recorded SHA-256 digest. An unchanged seeded directory is a no-op on rerun. If provisioning is interrupted before the manifest is written, the partial directory is deliberately left untouched and cannot be reseeded automatically; inspect it and select a fresh empty directory. The tool never removes existing data. Its credentials and code are isolated in the console project and are not registered in the Web application.

## Configuration and secrets

Never commit API keys or passwords. Configure development-only secrets with user-secrets after initializing the Web project:

```powershell
dotnet user-secrets init --project src/ApexPost.Web
dotnet user-secrets set "Integration:ApiKey" "<local-development-key>" --project src/ApexPost.Web
dotnet user-secrets set "Integration:AllowedOrigin" "https://<approved-courier-widget-origin>" --project src/ApexPost.Web
```

Configure `Integration:AllowedIps` as an array in environment configuration for the courier partner's observed client addresses. The API key is required for `POST /api/integrations/courier/shipments/{shipmentId}/status`; the request JSON is `{ "status": "In transit" }`. A successful response contains `shipmentId`, `status`, and `receivedAtUtc`. The route also enforces the configured IP allowlist and partner rate limit. CORS allows the configured browser origin; it does not authenticate callers. See [DESIGN.md](DESIGN.md) for reverse-proxy/IP trust details.

The staff MVC inventory inquiry is at `/Inventory/Index`. The counter sale form is `/Transactions/Create` for active Counter Clerks, Branch Managers, and Administrators. It accepts catalogue products and an optional parcel/letter shipping service; it reloads prices, taxes, stock, counter, branch and staff from server-side repositories. The shipping pricing rules and tax rates in `appsettings.json` are demonstration configuration and must be replaced with approved business values before operational use. Add destination zones under `Shipping:PricingRules` as needed. Successful sales are Paid immediately and use a receipt redirect. Branch Managers and Administrators can review transactions at `/Transactions`; branch authorization uses the current staff record, with Administrators allowed across branches. A void requires a reason and a Paid transaction. It records a void state/reason only; it does not create a refund or reverse inventory. JSON stock reservation is atomic within one application process; failure paths compensate inventory and shipment writes, but separate JSON files do not provide crash-atomic multi-file transactions. The demo accounts above are created only by the explicit local seed command. Use configuration files only for non-secret settings. Do not commit real credentials.

## Architecture

- `ApexPost.Domain`: business entities, value objects, aggregates, domain rules and domain events. No framework or infrastructure references.
- `ApexPost.Application`: use cases, DTOs, repository/service abstractions and orchestration. References Domain.
- `ApexPost.Infrastructure`: JSON repositories, audit store, notifications and external-service simulations. Implements Application abstractions.
- `ApexPost.Web`: MVC controllers, Razor views, authentication, middleware and filters. Composition root references Application and Infrastructure.
- `tests/`: domain/application unit tests and web integration tests.

See [DESIGN.md](DESIGN.md) and [docs/adr](docs/adr/).

The verified requirement status, security findings, and persistence limitations are recorded in [the security, persistence, and architecture audit](docs/SECURITY-PERSISTENCE-ARCHITECTURE-AUDIT.md).

## Git workflow

Create short-lived feature branches from `main`, make small commits, run build/tests, and open a pull request. Merge only reviewed, passing work. Keep secrets and local runtime data out of source control.

## Status

Implemented slices include staff cookie authentication, MVC inventory inquiry, a courier parcel-status integration, counter sales with retail and shipping lines, and transaction voiding. Other assignment workflows remain incomplete; use [docs/RUBRIC-CHECKLIST.md](docs/RUBRIC-CHECKLIST.md) and [DESIGN.md](DESIGN.md) for scope and architecture status.
