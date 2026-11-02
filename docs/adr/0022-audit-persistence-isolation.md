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

## Implementation and verification

See `SqlAuditStore`, `AuditActionFilter`, `AuditActionRecord`, and `SqlEntityMapper`. The filter logs write failures; SQL integration tests verify persistence, and focused Web tests verify safe audit data and that the filter does not replace the action result.
