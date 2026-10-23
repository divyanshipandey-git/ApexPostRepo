# Week 2 rubric checklist

Track each item as `Not started`, `In progress`, or `Verified`. Mark an item verified only when implementation and evidence exist.

## Domain refactor and business rules (20)
- [ ] Branches, counters, staff and role/status rules
- [ ] Products and branch-level inventory; no negative stock
- [ ] Shipping pricing and maximum service-level weight
- [ ] Money-order threshold and same-transaction successful verification
- [ ] Void/refund manager authorization and reason
- [ ] Closed branch rejection
- [ ] Four required reports with exact expected-value tests

## Advanced C# 14 (15)
- [ ] Records/value objects
- [ ] Pattern matching for real pricing/tax/service rules
- [ ] C# 14 extension members and `field` where appropriate
- [ ] Generic repository/specification with meaningful constraints
- [ ] Delegates/predicates for configurable rules
- [ ] Domain events/observer notifications
- [ ] Attribute/reflection validator with cached metadata
- [ ] Async/cancellation end-to-end
- [ ] `Task.WhenAll` for independent dashboard work
- [ ] `SemaphoreSlim` for bounded concurrency and safe JSON access
- [ ] `ValueTask`/cache justified with measurement or clear reasoning
- [ ] Cancellation/failure distinction and timeouts

## MVC (10)
- [ ] Login, dashboard, catalogue/stock, counter transaction, shipping label
- [ ] Money order/identity verification and report pages
- [ ] Conventional routing, PRG and correct action results
- [ ] Thin controllers and ViewModels

## Middleware/security/observability (20)
- [ ] Problem Details exception handling without production stack traces
- [ ] Correlation ID echoed and included in logs
- [ ] Structured request logging and timing in `try/finally`
- [ ] Maintenance mode with `503`, `Retry-After`, and `/health` exception
- [ ] Security headers, HTTPS and HSTS outside Development
- [ ] Endpoint-metadata API-key validation with constant-time comparison
- [ ] IP allowlist and forwarded-header/proxy risk documented
- [ ] Cookie authentication, claims, policies, antiforgery and secure cookie settings
- [ ] Login and partner rate limiting
- [ ] Restricted CORS policy and explanation that CORS is not authorization
- [ ] `/health`

## Filters/audit (15)
- [ ] `AuditActionFilter` and append-only `IAuditStore`
- [ ] Validation filter
- [ ] Performance filter
- [ ] Filter-versus-middleware rationale
- [ ] Sensitive data excluded from audit records

## Performance (10)
- [ ] No blocking async calls; all I/O async
- [ ] CancellationToken passed end-to-end
- [ ] Bounded concurrent access and no stock oversell
- [ ] Cache strategy and evidence

## Tests/docs/Git (10)
- [ ] Required domain unit tests with exact assertions
- [ ] At least six `WebApplicationFactory` integration tests
- [ ] At least five branches, 20 staff, 30 products, 10 zones/rates, 40 historical transactions
- [ ] README, DESIGN, at least six new ADRs, audit sample
- [ ] Feature branches, reviewed PRs, no secrets, clean .NET 10 build with zero warnings
