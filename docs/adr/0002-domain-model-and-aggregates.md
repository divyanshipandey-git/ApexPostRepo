# ADR-0002: Model business invariants with aggregates and value objects

- **Status:** Proposed
- **Context:** Stock cannot become negative; money orders above a threshold require verification; transaction state changes are restricted.
- **Decision:** Use value objects for concepts such as Money, Weight, Sku and BranchCode. Finalize aggregate boundaries before implementation, with explicit transaction, inventory, shipping and money-order rules.
- **Consequences:** Invalid state is rejected close to the domain model and unit tests can target rules without MVC or file I/O.
- **Open question:** Confirm whether inventory reservation and counter transaction completion need a single application-level consistency boundary for the JSON implementation.
