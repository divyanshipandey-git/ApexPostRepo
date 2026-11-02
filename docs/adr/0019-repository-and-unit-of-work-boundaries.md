# ADR 0019: Keep write repositories narrow and report reads projected

## Status

Accepted for the current Infrastructure/Application boundary.

## Context

The application needs replaceable persistence ports without leaking EF query composition into Application or MVC. Report reads also need provider-side filtering and projection to avoid loading full tables.

## Decision

Application defines repository and `IUnitOfWork` contracts. SQL repositories stage aggregate changes and do not call `SaveChangesAsync`; the use case commits. Application write use cases that persist through the SQL runtime require an `IUnitOfWork` dependency and cannot silently omit the commit. The Web composition root resolves it as a required service. JSON-only tests/adapters must opt into their persistence behavior explicitly rather than relying on a missing unit of work. `SqlReportDataSource` may use the scoped `ApexPostDbContext` directly for read-only DTO projections and stored procedure results because it is an Infrastructure query adapter, not an aggregate repository. No `IQueryable` crosses the Infrastructure boundary. `SqlAuditStore` is separately scoped to a factory-created context (see ADR-0022).

## Alternatives considered

- A generic repository that exposes `IQueryable`: rejected because provider semantics and deferred execution would leak.
- Force every query through aggregate repositories: rejected for report joins/grouping that return read DTOs rather than aggregates.
- Let each write repository save independently: rejected because it breaks one-use-case commit boundaries.

## Consequences

Application remains provider-neutral for report contracts and commands. Infrastructure owns EF translation and `AsNoTracking`. Direct DbContext use is limited to query adapters and persistence-specific batch operations.

## Implementation and verification

See `IReportDataSource`, `IUnitOfWork`, `SqlRepositories.cs`, `SqlUnitOfWork.cs`, and the scoped registrations in `SqlInfrastructureExtensions.cs`. Repository, unit-of-work, and SQL report tests are in `SqlServerIntegrationTests`; they use isolated catalogs when enabled.
