# ADR-0003: Keep JSON persistence behind asynchronous ports

- **Status:** Accepted
- **Context:** A database/ORM is prohibited this week, but next week's database must replace JSON without changing business logic.
- **Decision:** Define repository abstractions in Application and implement asynchronous JSON adapters in Infrastructure. Pass cancellation tokens through request operations. Use a per-full-path `SemaphoreSlim` and temporary-file replacement for single-entity writes; use compensating operations for multi-file counter sales. Do not use `.Result`, `.Wait()` or `.GetAwaiter().GetResult()`.
- **Consequences:** Storage is replaceable and testable. The lock is process-local and multi-file writes are not crash-atomic, so deployment remains single-instance and recovery/backup limits are documented.
