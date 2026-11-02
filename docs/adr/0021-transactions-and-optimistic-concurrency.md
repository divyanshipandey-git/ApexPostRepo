# ADR 0021: Use database transactions and rowversion for conflicting writes

## Status

Accepted for the SQL write paths; verify each new mixed operation before deployment.

## Context

Two clerks may sell the last stock unit concurrently, and sale/reversal writes must not persist partial transaction, line, payment, or stock state.

## Decision

Map `BranchStock` and `CounterTransactionRecord` rowversions and translate `DbUpdateConcurrencyException` into `ConcurrencyConflictException` at the unit-of-work boundary. A SQL counter sale stages stock, transaction, and any shipment or money order in one scoped context and commits them once inside `IUnitOfWork.ExecuteInTransactionAsync`; it does not compensate with deletes after a failed SQL write. JSON compatibility adapters retain separate compensation because they do not support atomic database transactions. The stock-adjustment procedure owns its own SQL transaction because its row lock and history insert are one procedure boundary. Concurrency failures are returned as safe HTTP 409 responses.

## Alternatives considered

- Process-local locks only: rejected because they do not coordinate multiple Web instances.
- Last-write-wins stock updates: rejected because it can oversell.
- Retry every concurrency exception invisibly: rejected because a sale should not silently change its quantity or price.

## Consequences

Callers must handle a stale-write result and ask the user to refresh/retry. Retrying an operation under an execution strategy requires the operation body to be database-only or otherwise idempotent.

## Implementation and verification

See `SqlUnitOfWork`, `CounterSaleService`, `SafeExceptionMiddleware`, `StockController`, and `20261103000000_HardenReportingProcedures.cs`. Application spy-UoW tests verify one transaction-boundary invocation for ordinary, shipment, and money-order sales; the standalone shipment test verifies one save. SQL tests cover stale stock updates, concurrent last-unit sales, rollback, and stock adjustment; SQL-backed verification remains opt-in and requires a configured isolated LocalDB connection.
