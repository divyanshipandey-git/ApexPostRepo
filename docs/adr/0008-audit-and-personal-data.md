# ADR-0008: Separate append-only business audit from diagnostic logs

- **Status:** Proposed
- **Context:** Important business actions must be attributable to a user, role, branch, time and correlation ID, while secrets and identity data must not leak.
- **Decision:** Use an IAuditStore abstraction with an append-only JSON implementation. Use ILogger<T> for diagnostics. Audit records exclude passwords, full document numbers, API keys and card data; document identifiers are masked before persistence.
- **Consequences:** Audit behavior is testable independently and has a clear privacy boundary. Append-only behavior and file-write concurrency need tests.
