# ADR 0021: Use database transactions and rowversion for conflicting writes

## Status

Accepted for the SQL write paths; verify each new mixed operation before deployment.

## Context

Two clerks may sell the last stock unit concurrently, and sale/reversal writes must not persist partial transaction, line, payment, or stock state.

## Decision

Map `BranchStock` and `CounterTransactionRecord` rowversions and translate `DbUpdateConcurrencyException` into `ConcurrencyConflictException` at the unit-of-work boundary. A counter sale stages stock and its transaction graph in one scoped context and saves once. Operations combining immediate SQL commands with tracked EF writes use `IUnitOfWork.ExecuteInTransactionAsync`, which executes through EF's configured retry strategy. The stock-adjustment procedure owns its own SQL transaction because its row lock and history insert are one procedure boundary. Concurrency failures are returned as safe HTTP 409 responses.

## Alternatives considered

- Process-local locks only: rejected because they do not coordinate multiple Web instances.
- Last-write-wins stock updates: rejected because it can oversell.
- Retry every concurrency exception invisibly: rejected because a sale should not silently change its quantity or price.

## Consequences

Callers must handle a stale-write result and ask the user to refresh/retry. Retrying an operation under an execution strategy requires the operation body to be database-only or otherwise idempotent.

## Implementation and verification

See `SqlUnitOfWork`, `CounterSaleService`, `SafeExceptionMiddleware`, `StockController`, and `20261102000000_AddReportFilters.cs`. Existing SQL tests cover stale stock updates, concurrent last-unit sales, rollback, and the stock adjustment procedure; the latest additions are opt-in and need a configured LocalDB test run.
