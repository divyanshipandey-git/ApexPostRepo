# ADR 0022: Persist audit entries outside the business unit of work

## Status

Accepted.

## Context

The MVC audit filter must record important actions, but audit storage failure must neither roll back a successful business operation nor hide a failed one. Audit payloads must not include secrets or full identity numbers.

## Decision

`SqlAuditStore` creates its own context through `IDbContextFactory`, writes one sanitized audit record, and commits independently after the MVC action result is known. `AuditActionFilter` catches an audit-store failure only at this boundary, logs the failure with action and correlation context, and leaves the business result unchanged. The audit mapper accepts only the safe allowlisted route values created by the filter.

## Alternatives considered

- Share the business request `DbContext` and transaction: rejected because audit failure could affect a sale and the filter runs after the use case.
- Drop audit failures silently: rejected because the audit gap needs an operational log.
- Put full action arguments or form bodies in audit rows: rejected because it risks logging credentials, identity information, and payment data.

## Consequences

Audit writes are durable independently of the business transaction but are not atomically coupled to it. An audit failure is observable in logs and may leave an audit gap; a durable retry/outbox mechanism is not implemented.

Business-row audit metadata is separate from `AuditEntries`: the current model has generic `CreatedAtUtc`/`CreatedBy` columns and selected `ModifiedAtUtc` shadow properties, but no persisted `ModifiedBy` property. The application records authenticated actor, role, action, and allowlisted route values in the independent `AuditEntries` row; stock movements also have their own `ChangedBy` field. No current business workflow or audit contract requires a second modifier identity on each transaction or money-order row. The generic `CreatedBy` column is nullable and is not populated by these sale workflows. Adding an unused `ModifiedBy` column would duplicate actor attribution without an identified business requirement, so the schema intentionally remains unchanged.

A submitted `ModifiedBy` value is not part of the MVC write contract, is not mapped by EF, and is not written. SQL-backed over-posting coverage submits forged `CreatedBy` and `ModifiedAtUtc` values, then verifies that the persisted creation time comes from the server, `CreatedBy` remains null, and an unmodified transaction's `ModifiedAtUtc` remains null. It also checks that the independent audit entries retain the authenticated staff ID and the actual server-generated transaction IDs rather than attacker-supplied values.

## Implementation and verification

See `SqlAuditStore`, `AuditActionFilter`, `AuditActionRecord`, and `SqlEntityMapper`. The filter logs write failures; SQL integration tests verify persistence, and SQL-backed Web tests verify actor attribution, safe route data, server-managed business-row audit columns, and that the filter does not replace the action result.
