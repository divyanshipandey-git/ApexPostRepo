# ADR-0002: Model business invariants with aggregates and value objects

- **Status:** Accepted
- **Context:** Stock cannot become negative; money orders above a threshold require verification; transaction state changes are restricted.
- **Decision:** Use value objects for Money, Weight, Sku and BranchCode. Keep transaction lifecycle and composed line items in `CounterTransaction`; enforce stock in `InventoryStock` and reserve it through the application/repository boundary. Shipment and money-order records reference their transaction instead of becoming nested mutable transaction state.
- **Consequences:** Domain invariants remain independently testable. The Application service coordinates inventory, shipment, money-order and transaction writes; with JSON storage, compensating writes handle ordinary failures but cannot make those files crash-atomic.
