# Assignment requirement traceability

This matrix maps the authoritative `apexpost.txt` requirements to implementation and verification. “Verified” means the referenced code and/or automated test was inspected or run in this checkout. Simulations and documented deployment limits are called out instead of being presented as production integrations.

## Part A — Functional requirements

| Spec | Requirement | Status | Evidence / limitation |
|---|---|---|---|
| 27 | Branch code, name, region, opening hours, active/closed status and counters | Verified | `ApexPost.Domain/Entities/Branch.cs`, `Counter.cs`; `BranchTests`, `CounterTests`, transaction service tests. Branch hours use host local time because the model has no branch time-zone field. |
| 30–32 | Add/update/deactivate staff; ID, name, email, role, home branch, active state; four roles | Verified | `StaffController`, `StaffService`, staff JSON repository and `/Staff`; `StaffServiceTests` and seed tests. Admin page is protected by role and configured IP allowlist. |
| 35–36 | Product catalogue fields/categories and stock per branch | Verified | `Product`, product repository, `/Products`, `/Stock`, `InventoryService`; product and inventory tests. |
| 39 | Parcel/letter, weight, dimensions, destination zone, service level and strategy pricing | Verified | `Shipment`, `ShippingService`, configured `ShippingPricingRule`s and `/Shipping/Label/{id}` preview; `ShippingPricingRuleTests`, `ShippingServiceTests`. The spec requires a destination zone, not a postal address; the preview is not carrier-ready. |
| 40–41 | Money order amount/sender/recipient/fee; threshold verification; document type/result and masked identifier only | Verified | `MoneyOrder`, `IdentityVerification`, `MoneyOrderService`, `/MoneyOrders/Create`; full document numbers are rejected. The check is staff-attested, not an external identity-provider verification. |
| 44–45 | Mixed basket, branch/counter/clerk, items, totals/tax/payment/status | Verified | `CounterTransaction`, line-item composition, `CounterSaleService`, `/Transactions/Create`; application/domain tests and Web integration coverage. Payment is simulated and a successful sale is immediately Paid. |
| 48 | Stock never negative; insufficient-stock sale rejected | Verified | Atomic all-lines reservation in `JsonInventoryRepository`; `InventoryStockTests`, `JsonInventoryRepositoryTests`, and insufficient-stock integration test. Same-process only; multi-process JSON writes are documented as unsafe. |
| 49 | Inactive staff blocked; clerk limited to assigned branch | Verified | Current staff record is reloaded and checked in Application services; `CounterSaleServiceTests`, `StaffServiceTests`, and authorization integration tests. |
| 50 | Above-threshold order needs successful identity verification in same transaction | Verified | `CounterSaleService` persists the verification with the linked order and paid transaction; integration tests cover missing/invalid verification and successful PRG. |
| 51 | Reject parcel above maximum weight for service level | Verified | `ShippingService` enforces `Shipping:MaximumWeightKgByServiceLevel` before shipment creation; `ShippingServiceTests.CalculatePrice_ServiceLevelLimit_AllowsBoundaryAndRejectsAbove` exercises exact and over-limit boundaries for all three levels. Demo limits (Standard 30 kg, Express 20 kg, Registered 10 kg) are configurable assumptions because the assignment gives no numeric values; replace them with approved business values. |
| 52 | Branch Manager void/refund with reason | Verified | `TransactionVoidService`, `TransactionRefundService`, transaction pages; tests cover authorization, reasons, state, stock restoration and retry behavior. JSON inventory restoration is idempotent within the running process. Administrator has the documented elevated cross-branch path. Refund is a state record, not a payment reversal. Older sales without SKU references cannot be restored automatically. |
| 53 | Closed branch cannot accept transaction | Verified | Branch/counter validation in `CounterSaleService`; integration coverage includes closed branch rejection. |
| 56 | Daily branch sales split retail/services | Verified | `ReportingService`, `JsonReportDataSource`, `/Reports`; reporting tests. Transactions without occurrence time are excluded. |
| 57 | Per-branch low-stock report and defined threshold | Verified | `JsonReportDataSource` reorder level 5, branch filters in `ReportingService`, `/Reports`; report tests. |
| 58 | Shipping volume by destination zone/service level | Verified | Shipment repository projection and `/Reports`; reporting tests. |
| 59 | Per-branch money orders and identity-verification auditor view | Verified | JSON money-order projection and `/Reports`; reporting tests. |
| 62 | JSON persistence behind repository abstractions; no DB/ORM | Verified | Infrastructure JSON repositories implement Application ports; persistence tests. Writes coordinate only within a process and multi-file flows are compensating, not crash-atomic. |
| 63 | Simulated low-stock, high-value order, every void and refund notification using observer/events | Verified | `INotificationService`, `SimulatedNotificationService.NotificationPublished`; calls after successful sale/stock adjustment/order/void/refund. Messages are in-memory and not durable. |

## Part B — Advanced C# 14 and performance

| Spec | Requirement | Status | Evidence / limitation |
|---|---|---|---|
| 68 | Records/value objects including Money, Weight, Address, BranchCode, Sku | Verified | Domain value objects/records are used across entities and services. |
| 69 | Pattern matching for pricing, tax and service-level checks | Verified | Shipping/tax/service rules use switch/pattern expressions; covered by domain and application tests. |
| 70 | C# 14 extension members and `field` | Verified | `CurrencyAmountExtensions` centralizes Money rounding; `InventoryStock.Quantity` uses `field` to enforce its invariant. .NET 10 SDK and `LangVersion 14.0` are configured. |
| 71 | Constrained generic repository plus report specification/filter | Verified | `IRepository<TEntity,TKey>`/`JsonFileStore<TEntity>` use constraints and key selectors; `ReportingService` applies branch, UTC date, category and reorder-level LINQ filters. No separate Specification type is needed for these simple report predicates. |
| 72 | Delegates/Func/Predicate for rules or filtering | Verified | `ShippingPricingRule` accepts a `Func<Weight, Money>` pricing policy used by the real `ShippingService` flow. `Shipping:PriceMultiplier` configures the default linear policy; `ShippingPricingRuleTests` proves alternate delegate policies produce different expected prices. |
| 73 | Events/observer notifications | Verified | `NotificationPublished` observer event is raised for all four required trigger types. |
| 74 | Attribute/reflection validator with cached metadata | Verified | `AttributeObjectValidator` recursively validates annotated requests and caches per-type metadata in a `ConcurrentDictionary`; focused tests cover valid and invalid nested data. |
| 75 | Async/await and cancellation from MVC to JSON repositories | Verified | Async service/repository paths accept and pass `CancellationToken`; non-cancelable tokens are used only for compensating cleanup after cancellation. |
| 76 | `Task.WhenAll` for independent dashboard/report work | Verified | `BranchDashboardService`, `ReportingService`, and `JsonReportDataSource` parallelize independent reads. |
| 77 | `SemaphoreSlim` for courier bounded concurrency and JSON writes | Verified | Courier lookup gate is limited to four operations; JSON store and inventory reservation use per-file gates within one process. |
| 78 | `ValueTask` plus cache; measure/explain benefit | Verified | Two-minute `ProductCatalogCache` returns synchronous completed `ValueTask` on warm SKU hits; measurement note compares warm cache with simulated delayed reads. It is illustrative, not a production benchmark. |
| 79 | Distinguish cancellation/failure and timeout slow lookup | Verified with simulation | Courier timeout maps to 504; caller cancellation remains cancellation. Delay wraps local JSON lookup/webhook processing; no outbound carrier client exists. Focused timeout/cancellation tests pass. |

## Part C — MVC

| Spec | Requirement | Status | Evidence / limitation |
|---|---|---|---|
| 82 | Login, branch dashboard, catalogue/stock, counter sale, shipping label, money order/identity forms and reports | Verified | `/Account/Login`, `/`, `/Products`, `/Stock`, `/Transactions/Create`, `/Shipping/Label/{id}`, `/MoneyOrders/Create`, `/Reports`. Identity fields share the money-order transaction form to preserve same-transaction verification. |
| 83 | Conventional routes, PRG after successful POST, correct MVC result types | Verified | Conventional route in `Program.cs`; mutation POSTs redirect after success and use typed views/404/400/403/status results as appropriate; integration tests cover representative flows. |
| 84 | Thin controllers; business rules in service/domain layers | Verified | Controllers bind ViewModels, call Application services and translate results; invariant tests target Application/Domain. |

## Part D — Middleware and security

| Spec | Requirement | Status | Evidence / limitation |
|---|---|---|---|
| 89 | Global safe Problem Details with trace ID, no production stack trace | Verified | `SafeExceptionMiddleware`; integration tests inspect error response. |
| 90 | Reuse/generate/echo correlation ID and include in logs | Verified | `CorrelationIdMiddleware` validates input, scopes structured logs and echoes header; integration test. |
| 91 | Structured request logging/timing, including failures | Verified | `RequestTimingMiddleware` logs in `finally`; integration coverage. |
| 92 | Configured maintenance mode, 503/Retry-After, health exemption | Verified | `MaintenanceModeMiddleware`; health/maintenance integration test. |
| 93 | CSP, nosniff, frame/referrer headers, HTTPS and non-Development HSTS | Verified | `SecurityHeadersMiddleware`, `Program.cs`; cross-cutting headers integration test. |
| 94–98 | Endpoint metadata API-key check; configured key; constant-time comparison; 401 invalid/missing; 500 unconfigured | Verified | `ApiKeyProtectedAttribute`, `EndpointProtectionMiddleware`; integration tests cover all three outcomes. Key comes from configuration/user-secrets. |
| 99 | Admin/integration IP allowlists return 403; proxy risk documented | Verified with configuration | `IpAllowlistProtectedAttribute` and middleware on courier/staff endpoints; blocked-IP tests and DESIGN proxy guidance. Missing allowlist fails closed. Forwarded headers are disabled. |
| 100–104 | Cookie auth, StaffId/Role/BranchCode claims, policies, antiforgery, HttpOnly/Secure/SameSite | Verified | Custom cookie authentication and authorization setup; antiforgery on POSTs; login/cookie/role/branch tests. |
| 105 | Login brute-force and per-partner rate limiting return 429 | Verified | Fixed-window policies; configured partner keys share a bucket and invalid requests partition by IP; integration tests cover login and partner limiting. |
| 106 | Restrict CORS to configured partner widget; explain CORS boundary | Verified | Named `PartnerIntegration` policy applied to courier endpoint; allow/deny origin integration test; README/DESIGN state CORS is not auth. |
| 107 | `/health` | Verified | Health check mapped anonymously and remains available during maintenance; integration test. |

## Part E — MVC filters and audit

| Spec | Requirement | Status | Evidence / limitation |
|---|---|---|---|
| 110–112 | At least three filters; audit is an `IAsyncActionFilter`, registered globally | Verified | `AuditActionFilter`, `ValidationActionFilter`, `PerformanceActionFilter`; scoped global registration in Web DI. DESIGN explains action context versus request-wide middleware. |
| 113–114 | Audit actor/role/branch/action/route/status/duration/correlation for required business actions | Verified for selected business POST actions | `AuditActionFilter` skips GET navigation, records explicit safe identifiers (transaction IDs, adjustment branch/SKU and shipment IDs), excludes generic route `id` and request bodies, and logs write failures without replacing completed responses; integration tests cover these behaviors. |
| 115–116 | Append-only JSON audit behind `IAuditStore`; no passwords/full ID/API keys/card data | Verified | `JsonAuditLog`, route-only audit filter and `AUDIT-SAMPLE.json`; test confirms no credentials/request body are written. |
| 117 | Consistent invalid-model response or typed view redisplay | Verified | `ValidationActionFilter` returns 400 without running ordinary invalid actions. Only MVC form actions marked with `RedisplayInvalidModelState` run to rebuild their view and options. Focused filter and form integration tests cover both paths. |
| 118 | Configurable slow-action warning | Verified | `PerformanceActionFilter` warns above `Observability:MVCSlowActionWarningMilliseconds`; default 500 ms. |

## Part F — Tests, documentation, seed data and constraints

| Spec | Requirement | Status | Evidence / limitation |
|---|---|---|---|
| 122 | xUnit tests for stock, money-order identity, parcel limits and pricing | Verified | Domain tests for stock, money-order policy/identity, shipment weight and pricing; Application tests cover service paths. |
| 123–129 | At least six WebApplicationFactory tests for listed security flows | Verified | 69 web integration tests; named tests cover headers, API key/IP, login 429, unauthenticated redirect, clerk void denial, correlation response, targeted audit, form redisplay and receipt shipment selection. |
| 130 | Feature branch, pull request, clear title, no secrets | Verified | Earlier assignment PR [#1](https://github.com/divyanshipandey-git/ApexPostRepo/pull/1) was merged. Targeted improvements are in PR [#3](https://github.com/divyanshipandey-git/ApexPostRepo/pull/3), open against verified `master` and not merged. The PR excludes `apexpost.txt`, `ApexPostWeek2.txt`, and `App_Data`. |
| 135–142 | Justified architecture/domain/basket/pricing/pipeline/filter/async/cache/DI/audit decisions | Verified | `DESIGN.md` and ADR-0001–0014 describe architecture, domain diagram, composition, strategy/rules, middleware/filter split/order, async/concurrency/cache, DI, privacy and JSON reversal recovery. ADRs 0002–0008 have been finalized as accepted decisions. |
| 145 | .NET 10 solution with Domain/Application/Infrastructure/Web/tests | Verified | `ApexPost.sln` includes required projects; build succeeds. |
| 146 | README run instructions, role demo logins, API key user-secrets and sample request/screenshots | Verified | README documents `dotnet run`, synthetic local credentials, user-secrets and sample courier request. No screenshots are included. |
| 147–153 | DESIGN architecture/domain/pipeline diagrams, filters table, >=6 ADRs, SOLID, security/audit | Verified | `DESIGN.md`, ADR folder and security audit. |
| 154–157 | Seed >=5 branches, 20 staff, 30 products, 10 zones/rates, 40 historical transactions | Verified for opt-in isolated seed | Demo seed tool and tests provision 5/20/30/10 zones (12 price rules)/40 paid transactions in an explicitly selected data directory. Web startup does not seed. Existing `App_Data` was not changed. |
| 158 | `AUDIT-SAMPLE.json` | Verified | Root sample is copied from an audit record written by the integration-tested filter and JSONL writer using isolated fictional demo data; fields use the writer's camelCase format. |
| 161 | .NET 10/C# 14, nullable enabled, zero-warning build | Verified | `global.json`, `Directory.Build.props`; latest `dotnet build ApexPost.sln --no-restore` succeeded with 0 warnings/errors. |
| 162–163 | JSON only, allowed library scope, no ASP.NET Core Identity | Verified | No DB/ORM or ASP.NET Core Identity package/use; MVC cookie authentication is implemented directly. |
| 164 | Program is composition/pipeline only | Verified | `Program.cs` registers services and middleware/routes; business logic is in Application/Domain. |
| 165 | No secrets in source control | Verified with bounded scan | No production API key or password found; synthetic demo credentials are explicitly documented. Configure real secrets outside source. |
| 166 | Asynchronous I/O; no `.Result`, `.Wait()`, `Thread.Sleep` | Verified | C# source scan under `src`, `tests`, and `tools` found no blocking async calls. |
| — | Manual browser/Visual Studio and GitHub review | Not yet verified | The local HTTP Kestrel smoke passed health/security-header/correlation and unauthenticated integration checks using an isolated temp dataset. Interactive browser login was not verified because no valid HTTPS development certificate is installed and this host cannot decrypt its existing DPAPI-protected Data Protection key. WebApplicationFactory covers authentication and role flows; there is no remote PR review. |

## Current known limitations

- Carrier status and timeout behavior are simulations around local JSON-backed webhook processing; there is no outbound courier client.
- Payment capture/reversal and partial refunds are outside the current model. A refund records transaction state and reason; it does not return funds through a provider.
- Shipment preview has the assignment's destination zone but no postal-address model, so it is not a carrier-ready label.
- Same-process semaphores and compensating writes do not provide multi-process coordination or crash-atomic multi-file transactions.
- Existing timestamp-free transactions are preserved and excluded from date-based reports.
- Administrator staff pages require `Security:AdminAllowedIps`; integration routes require API key and IP/origin configuration.
- Product cache misses are not coalesced and missing SKUs are not negatively cached; no catalogue write route currently exists. Future writes must call `Invalidate()`.
- Demo parcel limits are Standard 30 kg, Express 20 kg, and Registered 10 kg. They are independent configuration entries chosen for demonstration because the assignment supplies no numeric values; operational values require business approval.
- Notifications are in-memory observer messages, not durable or operator-visible.
- No remote CI checks or independent review approvals are configured; the PR was reviewed against the local diff and verified with the local build/test suite.
