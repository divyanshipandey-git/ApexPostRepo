# ADR 0023: Map HTTP inputs explicitly and keep report output as DTOs

## Status

Accepted for report screens and commands; remaining entity-returning catalogue screens are a documented gap.

## Context

MVC binding directly to business entities risks over-posting identifiers, roles, status, audit fields, or persistence state. Reports need filtered results, not tracked entity graphs.

## Decision

Use request/view models for MVC form posts and map only approved fields into Application requests. Report data sources return report DTOs and apply date/branch/threshold filters in SQL before materialization. Views never receive `IQueryable` or identity-document data. Keep response-model conversion close to the Web/Application boundary.

## Alternatives considered

- Bind EF/domain entities directly from POST bodies: rejected because clients could set server-owned fields.
- Return `IQueryable` to MVC for convenience: rejected because execution and filtering would leave the SQL boundary.
- Return full entities for report screens: rejected because it loads unnecessary fields and couples display to persistence.

## Consequences

Report queries have explicit projections and the report UI has stable no-record states. Some non-report read screens still expose domain `Product` values from `ProductCatalogService`; their conversion to dedicated response DTOs remains required before claiming that every read screen follows this decision.

## Implementation and verification

See `ReportsController`, `ReportsIndexViewModel`, `SqlReportDataSource`, and `PriceRevisionViewModel`. Report tests cover split totals, grouping, thresholds, empty dates, and validation; integration tests cover role-gated report access when the isolated SQL fixture is enabled. Existing over-posting tests are listed in `docs/RUBRIC-CHECKLIST.md`.
