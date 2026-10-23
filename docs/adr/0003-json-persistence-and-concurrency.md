# ADR-0003: Keep JSON persistence behind asynchronous ports

- **Status:** Proposed
- **Context:** A database/ORM is prohibited this week, but next week's database must replace JSON without changing business logic.
- **Decision:** Define repository abstractions in Application and implement asynchronous JSON adapters in Infrastructure. Use cancellation tokens and coordinated writes. Do not use `.Result`, `.Wait()` or `GetAwaiter().GetResult()`.
- **Consequences:** Storage is replaceable and testable. JSON has limited transaction guarantees, so multi-file consistency and crash recovery must be documented and tested.
