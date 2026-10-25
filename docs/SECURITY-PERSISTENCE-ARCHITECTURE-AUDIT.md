# Security, Persistence, and Architecture Audit

Audit basis: the complete `apexpost.txt`, current implementation, configuration, tests, and documentation on `feature/application-layer`. Requirement-by-requirement status and evidence are tracked in `docs/RUBRIC-CHECKLIST.md`; this document records security, persistence, and deployment limits.

## Security and Git findings

- **No real secret found in the scanned working-tree configuration/source.** `src/ApexPost.Web/appsettings.json` contains non-secret development settings and demonstration shipping/tax values; it does not configure a production API key or staff password. Integration-test credentials and the separately isolated demo seed credentials are synthetic. `UserSecretsId` is configured and README documents local API-key user-secrets. The Web application does not automatically seed credentials.
- Pattern scans of current tracked/untracked files and available Git history found no private-key blocks or common provider-token signatures. Credential-pattern matches are limited to synthetic test fixtures and the opt-in local demo seed account documented in README. This is not exhaustive secret detection; no secret values are included in this report.
- **Runtime-data ignore coverage was incomplete (fixed in this audit).** The default data directory is under the Web output directory, which is ignored as build output, but a configured repository-root `App_Data` or JSONL audit file could otherwise be added accidentally. `.gitignore` now ignores root `App_Data/` and `*.jsonl`, while retaining the pre-existing ignore entries.
- The working tree still contains uncommitted implementation, documentation and focused test changes, along with the pre-existing untracked `apexpost.txt` and `.vscode` content. No branch switch, commit, push, reset or history rewrite occurred in this session.
- A pattern-based scan is a useful check, not proof that arbitrary secrets or personal data are absent. No secret values are included in this report. If an actual credential is discovered later, rotate it; do not rewrite shared history without explicit authorization.

## JSON persistence behavior

`JsonPersistenceOptions.DataDirectory` defaults to the solution-root `App_Data` when `ApexPost.sln` is found above the running assembly; otherwise it falls back to `AppContext.BaseDirectory/App_Data`. Web composition binds the optional `JsonPersistence:DataDirectory` configuration key; an absolute path is recommended. `AddInfrastructure(Action<JsonPersistenceOptions>)` also supports explicit configuration. Entity files are created only when the first write occurs; a missing file reads as an empty collection. The opt-in `tools/ApexPost.DemoSeed` console project can populate an operator-selected existing empty directory; it is not called by Web startup and contains only synthetic accounts.

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
- DI lifetimes: JSON repositories/options/audit store/password hasher, `ProductCatalogCache`, simulated notification observer and `TimeProvider` are singleton; Application use cases, report data source, and MVC filters are scoped. Custom middleware constructors use singleton-safe dependencies. The Web configuration registers the configured shipping/courier services.
- `StaffService`, `ReportingService` and `JsonReportDataSource` are registered. Dashboard, reports, staff, catalogue, stock, label preview and standalone transaction-linked money-order routes consume their Application services.
- `AttributeObjectValidator` uses custom validation attributes and a concurrent per-type reflection metadata cache. `JsonEntityPropertyReader` remains a separate branch metadata adapter. `IRepository<TEntity,TKey>` and `JsonFileStore<TEntity>` use generic constraints/key selectors.
- `NotificationPublished` is implemented by the singleton simulated observer. Counter sales and stock adjustments notify low stock, high-value money orders notify after successful completion, and successful void/refund workflows publish their respective messages. Notifications are process-local; there is no durable queue or operator inbox.
- `PerformanceActionFilter` logs at Debug and warns above configurable `Observability:MVCSlowActionWarningMilliseconds`. `AuditActionFilter` records route/action and staff/correlation metadata without reading form data; `IAuditStore` appends JSONL in this single process.
- Valid configured partner API keys share a partner rate-limit bucket; invalid requests are partitioned by remote IP. Integration and administrator staff-management endpoints use configured IP allowlists. Forwarded headers remain disabled unless trusted proxies/networks are explicitly configured.
- Tax/shipping and money-order settings in `appsettings.json` remain demonstration values. Courier status handling has a configurable timeout, bounded concurrency and simulated delay, but there is no real outbound carrier client. Product cache TTL is two minutes; catalogue writes are not currently exposed in Web and any future write path must invalidate the cache.

## Requirement completion matrix

`Complete and verified` means implemented behavior has relevant tests. `Partial` means a real part exists but the full requirement does not. `Missing` means no implementation was found. `Not yet verified` means evidence is insufficient.

| Assignment requirement | Status | Evidence and remaining gap |
|---|---|---|
| Branches, counters, staff roles/status and lifecycle | Complete for assignment workflows | Domain and JSON records exist; administrators can add/update staff profiles and activate/deactivate accounts. Branch/counter administration screens are not part of the requested MVC list. |
| Product catalogue and branch stock; no negative inventory | Complete | `/Products` lists products; managers/admins adjust stock at `/Stock`; JSON mutation and sale reservation serialize through the same file gate. |
| Shipping pricing, service levels, weight limits | Complete with label limitation | Rule/entity/service and weight validation exist; appsettings has 10 demo zones/12 pricing rules; receipt links to a printable preview. Shipment model has no recipient postal address, so preview is not carrier-ready. |
| Money order threshold and same-transaction successful identity verification | Complete | `/Transactions/Create` and `/MoneyOrders/Create` use `CounterSaleService`, apply configured fee/threshold, require successful verification above threshold, and persist the order with the paid transaction. Identity check is staff attestation, not external provider validation. |
| Mixed counter transactions, payment, state | Complete within simulated payment scope | Paid retail, shipping and money-order lines, receipt, status lifecycle, void and full-refund state are implemented. No payment-provider capture/reversal or partial-refund support. |
| Inactive staff, assigned branch and closed-branch transaction rules | Complete | Counter-sale service checks current staff, branch, counter, opening state and branch assignment; existing tests cover rejects. |
| Manager void/refund with reason | Complete within domain scope | Current-staff role/branch checks, mandatory reasons, PRG, audit filter, simulated notifications and tests exist. No payment-provider reversal; void does not restore inventory. |
| Four required reports and auditor view | Complete | `JsonReportDataSource` connects JSON repositories to report DTOs; dashboard and `/Reports` show daily sales, low stock, shipping volume, and money orders/identity checks. Timestamp-free legacy transactions are excluded from date reports. |
| JSON persistence behind abstractions; no DB/ORM | Complete | JSON repositories and store tests exist. Cross-file crash consistency and multi-process writes remain limited as documented above. |
| Simulated low-stock/high-value/void/refund notifications | Complete for required triggers | Low-stock triggers on sales and adjustments; high-value orders, successful voids, and successful refunds publish observer events. Messages are in-memory and not durable/displayed. |
| Records/value objects and pattern matching | Complete | Domain values/entities and service rules are used. |
| C# 14 extension members / `field` | Complete | Currency rounding uses an extension member; stock's setter enforces non-negative quantity with `field`. The .NET 10 SDK and `LangVersion 14.0` compile both. |
| Generic repository/report filtering and delegates | Complete | Constrained generic JSON store and key/mutation delegates are used; `ReportingService` applies branch/date/category/reorder-level filters to report projections. The current simple filters do not need a separate Specification type. |
| Domain events/observer notifications | Complete for required notification paths | Singleton observer event is wired to low stock, high-value order, void and refund outcomes; notifications are process-local. |
| Reflection validator with cached metadata | Complete | Custom request attributes are validated recursively; per-type property/attribute metadata is cached in a concurrent dictionary. |
| Async/cancellation end to end | Complete for request and repository workflows | MVC/application/repository APIs pass cancellation; cleanup may use non-cancelable compensation intentionally. |
| `Task.WhenAll`, `SemaphoreSlim`, `ValueTask`/cache | Complete | Dashboard/report fan-out, JSON/inventory/courier gates, and a two-minute product `ValueTask` cache are used. A local latency demonstration is documented; it is not a production benchmark. |
| Cancellation/failure distinction and external lookup timeout | Partial simulation | Courier status flow distinguishes caller cancellation from configured timeout and maps timeout to 504. Delay is simulated around local JSON lookup; no outbound carrier client exists. |
| MVC login, inventory, counter transactions, void pages | Complete for requested pages | Login, dashboard, catalogue, stock management, counter sale, linked money-order/identity flow, label preview, reports, void/refund and transaction details are present. |
| Correct MVC results and thin controllers | Complete for current routes | Controllers coordinate Application services and use ViewModels; business invariants remain in Domain/Application. |
| Exception handling, correlation, timing/logging, maintenance, headers, HTTPS/HSTS, health | Complete and integration tested | Middleware is registered in the documented order; integration cases cover safe errors, correlation/security headers, maintenance and health. |
| API key, IP restrictions, cookie claims/roles, antiforgery, rate limiting, CORS | Complete with configuration requirements | Courier uses key/IP/CORS/rate limits; staff admin endpoints use `Security:AdminAllowedIps`; partner key receives shared rate bucket; admin allowlist and secrets must be configured. |
| Three filters and audit protection | Complete | Audit, validation and performance filters are global; slow-action threshold is configurable; append-only store avoids request body/action data. |
| Required xUnit domain tests and >=6 Web integration tests | Complete and verified | Domain/application suites plus 62 Web integration cases passed in the current run. |
| Opt-in demo seed volume (5 branches, 20 staff, 30 products, 10 zones/rates, 40 transactions) | Complete for new seed runs | Guarded console tool provisions required counts; new sample transactions carry UTC timestamps. Existing timestamp-free `App_Data` data was preserved and not migrated. |
| README, DESIGN, six new ADRs, audit sample and diagrams | Complete | README, architecture/domain/pipeline diagrams, SOLID/security/filter/DI sections, ADR-0001 through ADR-0013, performance notes and `AUDIT-SAMPLE.json` exist. |
| Git workflow and zero-warning .NET 10 build | Build verified; review workflow pending | Current feature branch is 7 commits ahead/0 behind; working tree remains uncommitted and no PR/push was made. |

## Remaining gaps and operational limits

The assignment workflows are implemented. Operational/integration limits remain: there is no real carrier tracking client, payment-provider integration, or partial-refund model. The label preview has the assignment-required destination zone but no postal-address fields, so it is not carrier-ready. Identity verification is captured with the money-order transaction to enforce the same-transaction rule. Admin staff pages require `Security:AdminAllowedIps`; timestamp-free legacy transactions remain unchanged and are excluded from date-based reports. GitHub review/PR workflow has not been performed in this local pass.

## Audit verification

- Domain suite: 110 discovered and executed, 110 passed, 0 failed, 0 skipped; exit code 0.
- Application suite: 164 discovered and executed, 164 passed, 0 failed, 0 skipped; exit code 0.
- Web integration suite: 62 discovered and executed, 62 passed, 0 failed, 0 skipped; exit code 0.
- Full `dotnet test`: 336 discovered and executed (Domain 110, Application 164, Web integration 62), 336 passed, 0 failed, 0 skipped; exit code 0.
- `dotnet build ApexPost.sln --no-restore`: succeeded, 0 warnings and 0 errors; exit code 0.
- `git diff --check`: exit code 0. Git emitted line-ending conversion notices for existing CRLF/LF working-copy differences; no whitespace errors were reported.

Re-run these commands after later code changes.
