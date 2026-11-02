# Exception handling and transaction review

## Review outcome

The SQL/MVC request path now distinguishes expected concurrency conflicts and client cancellation from unexpected failures. Errors are not converted into successful-looking responses. Unexpected failures are logged and returned as generic Problem Details rather than exposing database or implementation details.

## Handling matrix

| Failure | Handling | User-visible result | Evidence |
|---|---|---|---|
| EF `DbUpdateConcurrencyException` during unit-of-work save | Clear the failed change tracker and translate to `ConcurrencyConflictException`. | HTTP 409 Problem Details with a refresh/retry message and trace ID. | `SqlUnitOfWork`, `SafeExceptionMiddleware`, middleware tests. |
| SQL Server unique-key violations (2601/2627) during unit-of-work save | Translate the provider error to `UniqueKeyConflictException` without exposing the SQL message. | HTTP 409 Problem Details with a generic duplicate-record message. | `SqlUnitOfWork`, `SafeExceptionMiddleware`, SQL and middleware tests. |
| Application `ValidationException` | Map the exception to a safe client error at the HTTP boundary. | HTTP 400 Problem Details without the exception message. | `SafeExceptionMiddleware` and middleware tests. MVC model validation remains the normal boundary. |
| `KeyNotFoundException` | Map the missing resource to a safe not-found response. | HTTP 404 Problem Details without the exception message. | `SafeExceptionMiddleware` and middleware tests. |
| `ConcurrencyConflictException` from a use case | Handle explicitly in the affected operation or middleware; do not treat it as ordinary invalid input. | HTTP 409, including the stock-adjustment redisplay path. | `StockController`, `CounterSaleService`, middleware tests. |
| Request-abort `OperationCanceledException` | Log at Debug and rethrow so cancellation is not misreported as a server error. Counter-sale compensation uses a non-cancelable token after a reservation to avoid leaving it stranded. | Request terminates as canceled; no success fallback is returned. | `SafeExceptionMiddleware`, `CounterSaleService`. |
| Unexpected exception before the response starts | Log the exception with trace context, clear the response, and return a generic `application/problem+json` response. | HTTP 500 with a generic title and trace/correlation identifiers; no exception detail. | `SafeExceptionMiddleware`. |
| Exception after the response has started | Do not attempt to replace a partially written response; allow the exception to propagate to the hosting server. | The host terminates the incomplete response. | `SafeExceptionMiddleware` response-started guard. |
| Audit persistence failure | Log the failure and preserve the already-determined business response. | The business response is not replaced by an audit-write error. | `AuditActionFilter`, `SqlAuditStore`, audit failure integration test. |
| SQL counter-sale persistence failure | Execute stock reservation, transaction/lines/payment, and linked shipment/money-order staging under one `IUnitOfWork.ExecuteInTransactionAsync` boundary. The SQL unit of work rolls back failed writes; no compensating delete is issued. | Explicit persistence-failure result or concurrency conflict; the database has no partial sale. | `CounterSaleService`, `SqlUnitOfWork`, SQL integration test. |
| Legacy JSON counter-sale persistence failure | The JSON adapter still performs independent file writes and uses a compensating cleanup path. It is not the Web runtime and cannot provide SQL transaction guarantees. | Cleanup errors are surfaced with the original operation error retained. | `CounterSaleService`, legacy JSON tests. |
| SQL stock-adjustment validation or constraint error | SQL procedure rolls back its transaction and rethrows. Application-level invalid-operation failures are returned as validation feedback; raw SQL exceptions reach the generic error boundary. | Validation response for known business input errors; otherwise safe HTTP 500. | `usp_AdjustStock`, `StockController`, SQL rollback test. |

## Findings corrected during this review

- The middleware previously did not recognize `ConcurrencyConflictException`, so an optimistic concurrency failure could be returned as HTTP 500. It now maps both the application wrapper and direct EF concurrency exception to HTTP 409.
- A stock-controller catch could classify a concurrency conflict as a client input error. It now handles concurrency explicitly before the general `InvalidOperationException` path.
- Request cancellation is no longer swallowed by a generic exception response when the client has disconnected.
- Audit persistence errors are logged without replacing an already-completed business response.
- SQL counter-sale writes now stage sale, stock, shipment, and money-order state in the same SQL transaction; the JSON compatibility path retains its existing compensation behavior.
- The stock-adjustment SQL procedure now has an explicit `TRY/CATCH`, rollback, and `THROW` boundary in addition to `XACT_ABORT`.
- Unique-key database errors are translated at the unit-of-work boundary before they reach HTTP middleware.
- Validation and missing-resource exceptions now map to safe HTTP 400 and 404 Problem Details responses.

## Residual considerations

- A failed database operation is not silently retried by the HTTP layer. SQL Server retry behavior is limited to the configured EF execution strategy and its supported transient failures.
- Audit writes are intentionally best-effort relative to the business response; a failed audit write is observable in logs but does not roll back an already-committed business action.
- If an exception occurs after response output starts, middleware cannot safely replace that output. The host remains responsible for terminating the incomplete response.
- Production monitoring must collect the structured trace/correlation identifiers and server-side logs; clients receive no internal exception details.
