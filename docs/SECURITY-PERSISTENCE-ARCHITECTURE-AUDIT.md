# Security, Persistence, and Architecture Audit

Audit basis: the supplied `apexpost.txt`, current implementation and tests on `feature/application-layer`. This is a verified scope baseline, not a claim that the assignment is complete.

## Security and Git findings

- **No real secret found in the scanned working-tree configuration/source.** `src/ApexPost.Web/appsettings.json` contains non-secret development settings and demonstration shipping/tax values; it does not configure a production API key or staff password. Integration-test credentials and the separately isolated demo seed credentials are synthetic. `UserSecretsId` is configured and README documents local API-key user-secrets. The Web application does not automatically seed credentials.
- Pattern scans of current tracked/untracked files and available Git history found no private-key blocks or common provider-token signatures. Credential-pattern matches are limited to synthetic test fixtures and the opt-in local demo seed account documented in README. This is not exhaustive secret detection; no secret values are included in this report.
- **Runtime-data ignore coverage was incomplete (fixed in this audit).** The default data directory is under the Web output directory, which is ignored as build output, but a configured repository-root `App_Data` or JSONL audit file could otherwise be added accidentally. `.gitignore` now ignores root `App_Data/` and `*.jsonl`, while retaining the pre-existing ignore entries.
- The working tree contained numerous modified and untracked implementation, documentation, test, `.gitignore`, and `.vscode` files during the initial audit. The implementation was finalized in reviewed dependency groups; `apexpost.txt` and `.vscode/settings.json` remain uncommitted. No branch switch, push, reset, or history rewrite occurred.
- A pattern-based scan is a useful check, not proof that arbitrary secrets or personal data are absent. No secret values are included in this report. If an actual credential is discovered later, rotate it; do not rewrite shared history without explicit authorization.

## JSON persistence behavior

`JsonPersistenceOptions.DataDirectory` defaults to `AppContext.BaseDirectory/App_Data`. Web composition now binds the optional `JsonPersistence:DataDirectory` configuration key; an absolute path is recommended. `AddInfrastructure(Action<JsonPersistenceOptions>)` also supports explicit configuration. Entity files are created only when the first write occurs; a missing file reads as an empty collection. The opt-in `tools/ApexPost.DemoSeed` console project can populate an operator-selected existing empty directory; it is not called by Web startup and contains only synthetic accounts.

| Repository data | File under the configured data directory |
|---|---|
| Branches | `branches.json` |
| Counters | `counters.json` |
| Staff | `staff.json` |
| Password hashes | `staff-credentials.json` |
| Products | `products.json` |
| Inventory | `inventory.json` |
| Counter transactions | `counter-transactions.json` |
| Shipments | `shipments.json` |
| Money orders | `money-orders.json` |
| MVC action audit | `audit.jsonl` |

Repositories wrap `JsonFileStore<TEntity>`; Web/Application use repository ports rather than reading JSON directly. `System.Text.Json` is configured case-insensitively, writes indented JSON, and serializes enums as strings. Domain private-set properties needed for persistence use explicit JSON inclusion. The audit store appends one JSON object per line.

Each entity-store write serializes to a unique temporary file in the destination directory, flushes, then moves it over the destination. A static `SemaphoreSlim` keyed by full path coordinates instances of the same closed `JsonFileStore<TEntity>` type within one process. It is not an interprocess lock. A missing entity file becomes an empty list; malformed JSON, access denial, and I/O errors propagate. Web turns unhandled request failures into generic Problem Details; callers outside Web see the persistence failure. A process termination can leave a temporary file. A crash during audit append can leave an incomplete final JSONL record.

Inventory reservation checks all requested lines and changes the inventory file under its per-file lock. A counter sale subsequently writes an optional shipment and a transaction; failure handling compensates some prior writes. Those compensations do not make the three files crash-atomic. Concurrent separate processes can overwrite one another because the lock is in-memory. Production multi-instance use needs an interprocess coordination/recovery design or a transactional store. There is no database/ORM. Data directory access control, backups, and recovery are deployment responsibilities.

Within the running process, inventory reservations now return opaque-by-registration tokens tracked by the JSON repository. A sale commits the token after the transaction is written, or restores it once during compensation. The repository rejects malformed quantities, unknown/modified tokens, and repeated restores. Reservation tokens are process-local; they do not provide crash recovery or cross-process coordination.

Transaction listing, details and void actions resolve the authenticated `StaffId` against the current staff repository record. A disabled account or a changed role/home branch cannot continue to void using stale cookie claims. Branch Managers remain restricted to their current home branch; Administrators retain cross-branch access.

## Architecture and bounded findings

- References are `Web -> Application, Infrastructure`, `Infrastructure -> Application, Domain`, `Application -> Domain`; Domain has no project references. Controllers invoke Application services and return MVC results; Razor pages use ViewModels. JSON filesystem access is in Infrastructure.
- DI lifetimes: repository implementations, JSON options, audit store, and password hasher are singleton; Application services and MVC filters are scoped; `TimeProvider` and the stateless branch-authorization handler are singleton. The Web configuration replaces the default `ShippingService` registration with one using configured pricing.
- **Application registrations remain incomplete for unimplemented workflows.** `StaffService` is not registered; `ReportingService` has no registered `IReportDataSource` implementation; no report MVC endpoints consume it. `MoneyOrderService` is registered and used by the counter-sale workflow. The counter-sale/void, inventory, authentication, money-order, and courier services used by current routes are registered.
- `JsonEntityPropertyReader` uses reflection to extract branch metadata for report adapters, but no reflection-based validation attribute/cache is present. `IRepository<TEntity,TKey>` has meaningful generic constraints; the JSON store also uses a key selector and mutation delegate.
- `NotificationPublished` is implemented by the singleton `SimulatedNotificationService`; counter sales publish low-stock (reorder threshold 5) and high-value money-order notifications, and successful void/refund workflows publish their respective messages. These remain process-local simulated notifications with no durable queue or operator notification page.
- `PerformanceActionFilter` logs action duration at Debug for every MVC action. It does not implement the rubric's configurable slow-action warning threshold. `AuditActionFilter` records route/action and staff/correlation metadata but cannot cover absent workflows. `IAuditStore` writes append-only JSONL in this single process.
- Partner rate limiting currently partitions by remote IP, not a partner identity/key. This is adequate only while the deployment has one partner identity per observed IP policy; it is not a general per-partner partition. IP checks use `RemoteIpAddress`; forwarded headers are intentionally not trusted/configured. Keep the documented trusted-proxy requirement.
- Demonstration tax/shipping and money-order settings in `appsettings.json` are not operational tariffs, fees, identity-policy guidance, or tax advice. No real courier-rate provider, cache, or timeout-based external lookup is present.

## Requirement completion matrix

`Complete and verified` means implemented behavior has relevant tests. `Partial` means a real part exists but the full requirement does not. `Missing` means no implementation was found. `Not yet verified` means evidence is insufficient.

| Assignment requirement | Status | Evidence and remaining gap |
|---|---|---|
| Branches, counters, staff roles/status and lifecycle | Partial | Domain entities and JSON repositories exist; staff status service exists and the demo tool provisions synthetic records. No branch/counter/staff administration MVC screens. |
| Product catalogue and branch stock; no negative inventory | Partial | Domain rules, repositories, inventory inquiry, atomic in-process reservations and tests exist. No catalogue management/stock adjustment workflow. |
| Shipping pricing, service levels, weight limits | Partial | Rule/entity/service and counter-sale validation exist; appsettings contains 10 demonstration zones/12 pricing rules. No label-preview page or operational rate provider. |
| Money order threshold and same-transaction successful identity verification | Complete and verified | `/Transactions/Create` accepts a money-order service, applies configured fee/threshold, requires a successful masked identity-verification record above threshold, and stores the money order with its paid transaction and branch. Application and Web integration tests cover association, missing verification, full-number rejection, and write-failure compensation. The recorded check is staff attestation, not an external identity-provider verification. |
| Mixed counter transactions, payment, state | Partial | Paid retail, shipping and money-order service lines, receipt, and persistence are implemented. Refunds are absent. |
| Inactive staff, assigned branch and closed-branch transaction rules | Complete and verified | Counter-sale service checks staff/branch/counter state and branch access; Application/Web tests cover relevant rejects. |
| Manager void/refund with reason | Partial | Paid transaction void and full-refund state changes have current-staff role/branch checks, mandatory reasons, PRG, audit-filter coverage, simulated notifications and focused tests. No payment-provider reversal or partial refund model exists; void does not refund or reverse inventory. |
| Four required reports and auditor view | Partial | Application report calculations and tests cover daily sales, low stock, shipping volume, and money orders/verification activity. No production report data-source registration or report MVC pages. |
| JSON persistence behind abstractions; no DB/ORM | Complete and verified | JSON repositories and store tests exist. Cross-file crash consistency and multi-process writes remain limited as documented above. |
| Simulated low-stock/high-value/void/refund notifications | Partial | Existing simulated event service is wired to sale, money-order, void and refund workflows. Notifications are in-memory and are not durable or displayed in an operations UI. |
| Records/value objects and pattern matching | Complete and verified | Domain values/entities and service rules have unit tests. |
| C# 14 extension members / `field` | Missing | No use found in source. |
| Generic repository/specification and delegates | Partial | Constrained generic repository port and key/mutation delegates exist; no report specification/filter abstraction. |
| Domain events/observer notifications | Partial | In-memory simulated notification event exists but is not connected to workflows. |
| Reflection validator with cached metadata | Missing | No such validator found; branch property reader reflection is unrelated and uncached. |
| Async/cancellation end to end | Partial | Repository and service APIs are async and generally accept cancellation. JSON temporary-file cleanup uses synchronous `File.Delete`; no timeout-based external lookup exists. |
| `Task.WhenAll`, `SemaphoreSlim`, `ValueTask`/cache | Partial | `Task.WhenAll` is used for independent report reads; semaphores protect in-process JSON access and rate limiting. No `ValueTask` cache or measurement exists. |
| Cancellation/failure distinction and external lookup timeout | Partial | Cancellation tokens are passed through core flows; no simulated courier-rate/tracking lookup with explicit timeout exists. |
| MVC login, inventory, counter transactions, void pages | Partial | Cookie login, inventory inquiry, sale/receipt with integrated money-order and identity-check form, and transaction details with void/full-refund forms exist with antiforgery and PRG tests. Branch dashboard, catalogue, shipping label, and report pages are absent. |
| Correct MVC results and thin controllers | Complete and verified for implemented routes | Current views/actions use typed models and application services; absent pages/workflows remain. |
| Exception handling, correlation, timing/logging, maintenance, headers, HTTPS/HSTS, health | Complete and verified for tested paths | Middleware is registered; Web integration tests cover safe errors, correlation/security headers, maintenance and health. |
| API key, IP restrictions, cookie claims/roles, antiforgery, rate limiting, CORS | Partial | Courier integration and login paths have tests for API key/IP/CORS/rate limits; cookie auth/role tests exist. Partner limiter partitions by IP, not partner key. No admin IP-protected route exists. |
| Three filters and audit protection | Partial | Audit, validation, performance filters and append-only store exist; audit tests verify safe metadata. Slow-action threshold and audit coverage for absent workflows are missing. |
| Required xUnit domain tests and >=6 Web integration tests | Complete and verified | Domain/application suites plus 60 Web integration cases currently pass; see final verification below. |
| Opt-in demo seed volume (5 branches, 20 staff, 30 products, 10 zones/rates, 40 transactions) | Partial | A guarded console tool and tests provision the requested counts into an isolated directory with synthetic credentials. Transactions lack occurrence timestamps, so the rows cannot truthfully serve as historical report data. |
| README, DESIGN, six new ADRs, audit sample and diagrams | Partial | README, DESIGN, nine ADR files, and `AUDIT-SAMPLE.json` exist. Eight ADRs remain Proposed; ADR-0009 records the accepted transaction-linked money-order decision. DESIGN has an architecture diagram but no domain/pipeline diagram or updated SOLID mapping. |
| Git workflow and zero-warning .NET 10 build | Not yet verified | Current branch is `feature/application-layer`; no history actions were taken. Build/test verification is recorded after this audit's changes. |

## Remaining-work estimate and next step

The foundation and implemented counter/security slices are testable, but the original assignment is not complete. Remaining work includes reports/dashboard, staff/catalogue administration, shipping preview, refunds, dated sample transactions, notification wiring, and missing C# 14/performance demonstrations. Treat this as multiple feature-sized tasks plus integration/documentation work. The next recommended slice is **transaction refund with role/branch authorization, reason capture, audit, and notification**, distinct from voiding.

## Audit verification

- Domain suite: 110 discovered and executed, 110 passed, 0 failed, 0 skipped; exit code 0.
- Application suite: 154 discovered and executed, 154 passed, 0 failed, 0 skipped; exit code 0.
- Web integration suite: 60 discovered and executed, 60 passed, 0 failed, 0 skipped; exit code 0.
- Full `dotnet test`: 324 discovered and executed (Domain 110, Application 154, Web integration 60), 324 passed, 0 failed, 0 skipped; exit code 0.
- `dotnet build ApexPost.sln --no-restore`: succeeded, 0 warnings and 0 errors; exit code 0.
- `git diff --check`: exit code 0. Git emitted line-ending conversion notices for existing CRLF/LF working-copy differences; no whitespace errors were reported.

Re-run these commands after later code changes.
