# ADR 0013: Courier timeout and transaction-linked money-order form

## Status

Accepted

## Context

Courier status processing needs bounded work, timeout handling, and separate caller cancellation. Staff also need a focused money-order form without duplicating the counter-sale business rules or weakening the same-transaction identity requirement.

## Decision

- Courier status updates use a shared four-operation semaphore, a linked request/timeout cancellation token, and a configurable simulation delay. A service timeout becomes HTTP 504; request cancellation propagates as cancellation. The current application accepts a partner status webhook and uses JSON persistence; it does not make a real outbound carrier tracking request.
- `/MoneyOrders/Create` submits a service-only counter sale through `CounterSaleService`, including any identity verification in the same transaction. It does not write a money order through a separate persistence path.

## Consequences

- The endpoint can demonstrate a slow-courier timeout without blocking threads, and its simulation delay defaults to zero.
- Money-order fee, identity policy, inventory/transaction persistence, audit filtering, and notification behavior remain centralized in the existing application workflow.
- The label and money-order records remain limited by the current domain's missing recipient postal-address fields and non-atomic multi-file persistence.
