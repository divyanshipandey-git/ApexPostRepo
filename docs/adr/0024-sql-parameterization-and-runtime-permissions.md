# ADR 0024: Parameterize SQL and separate runtime permissions from deployment

## Status

Accepted.

## Context

The SQL runtime accepts branch, date, SKU, threshold, and adjustment values from application workflows. Database credentials must remain outside source control, and the runtime should not own schema deployment.

## Decision

Use EF Core LINQ, `SqlQuery` interpolation, and `ExecuteSqlInterpolatedAsync` for all application SQL. Do not concatenate request data into commands. Supply the Development connection through user-secrets/environment configuration. Grant the runtime identity procedure `EXECUTE` rights and only the mapped-table DML needed for its workflows; use a separate deployment identity for migrations. Keep the Web startup fail-closed when SQL configuration is missing.

## Alternatives considered

- Embed credentials or API keys in `appsettings.json`: rejected because source control is not a secret store.
- Use `db_owner` for the runtime: rejected because application requests should not change schema.
- Construct dynamic SQL by concatenating user input: rejected as unsafe and unnecessary for current report shapes.

## Consequences

Deployment must provision the runtime role and migration identity separately. Permission changes must track repository operations; procedure `EXECUTE` grants alone are not sufficient because EF repositories also access tables.

## Implementation and verification

See `SqlInfrastructureExtensions`, `ApexPostDesignTimeDbContextFactory`, and [database/README.md](../../database/README.md). Existing integration/security tests verify parameterized SQL flows and fail-closed configuration; the actual deployment grants remain environment-specific and are not applied by this repository.
