# Exception handling and transaction review

## Review outcome

The SQL/MVC request path now distinguishes expected concurrency conflicts and client cancellation from unexpected failures. Errors are not converted into successful-looking responses. Unexpected failures are logged and returned as generic Problem Details rather than exposing database or implementation details.

## Handling matrix

| Failure | Handling | User-visible result | Evidence |
|---|---|---|---|
| EF `DbUpdateConcurrencyException` during unit-of-work save | Clear the failed change tracker and translate to `ConcurrencyConflictException`. | HTTP 409 Problem Details with a refresh/retry message and trace ID. | `SqlUnitOfWork`, `SafeExceptionMiddleware`, middleware tests. |
| `ConcurrencyConflictException` from a use case | Handle explicitly in the affected operation or middleware; do not treat it as ordinary invalid input. | HTTP 409, including the stock-adjustment redisplay path. | `StockController`, `CounterSaleService`, middleware tests. |
| Request-abort `OperationCanceledException` | Log at Debug and rethrow so cancellation is not misreported as a server error. Counter-sale compensation uses a non-cancelable token after a reservation to avoid leaving it stranded. | Request terminates as canceled; no success fallback is returned. | `SafeExceptionMiddleware`, `CounterSaleService`. |
| Unexpected exception before the response starts | Log the exception with trace context, clear the response, and return a generic `application/problem+json` response. | HTTP 500 with a generic title and trace/correlation identifiers; no exception detail. | `SafeExceptionMiddleware`. |
| Exception after the response has started | Do not attempt to replace a partially written response; allow the exception to propagate to the hosting server. | The host terminates the incomplete response. | `SafeExceptionMiddleware` response-started guard. |
| Audit persistence failure | Log the failure and preserve the already-determined business response. | The business response is not replaced by an audit-write error. | `AuditActionFilter`, `SqlAuditStore`, audit failure integration test. |
| Counter-sale persistence failure | Attempt compensating cleanup with a non-cancelable token. Log the original persistence error if compensation completes; if compensation fails, throw an `InvalidOperationException` with both failures retained in an `AggregateException`. | Explicit persistence-failure result or generic HTTP 500; concurrency conflicts are separately reported. | `CounterSaleService`. |
| SQL stock-adjustment validation or constraint error | SQL procedure rolls back its transaction and rethrows. Application-level invalid-operation failures are returned as validation feedback; raw SQL exceptions reach the generic error boundary. | Validation response for known business input errors; otherwise safe HTTP 500. | `usp_AdjustStock`, `StockController`, SQL rollback test. |

## Findings corrected during this review

- The middleware previously did not recognize `ConcurrencyConflictException`, so an optimistic concurrency failure could be returned as HTTP 500. It now maps both the application wrapper and direct EF concurrency exception to HTTP 409.
- A stock-controller catch could classify a concurrency conflict as a client input error. It now handles concurrency explicitly before the general `InvalidOperationException` path.
- Request cancellation is no longer swallowed by a generic exception response when the client has disconnected.
- Audit persistence errors are logged without replacing an already-completed business response.
- Counter-sale persistence failures are logged after successful compensation, and a compensation failure retains both the original and rollback exceptions.
- The stock-adjustment SQL procedure now has an explicit `TRY/CATCH`, rollback, and `THROW` boundary in addition to `XACT_ABORT`.

## Residual considerations

- A failed database operation is not silently retried by the HTTP layer. SQL Server retry behavior is limited to the configured EF execution strategy and its supported transient failures.
- Audit writes are intentionally best-effort relative to the business response; a failed audit write is observable in logs but does not roll back an already-committed business action.
- If an exception occurs after response output starts, middleware cannot safely replace that output. The host remains responsible for terminating the incomplete response.
- Production monitoring must collect the structured trace/correlation identifiers and server-side logs; clients receive no internal exception details.
