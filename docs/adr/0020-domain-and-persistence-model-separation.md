# ADR 0020: Separate persistence records where storage shape differs

## Status

Accepted for the current EF model.

## Context

The Domain model should enforce business rules without adopting persistence-only fields or SQL row shapes. Some existing aggregates map directly; others have relationships or snapshots that do not match one domain object.

## Decision

Keep direct Fluent mappings for stable domain records such as `Branch`, `Product`, and `InventoryStock`. Use separate Infrastructure persistence records and `SqlEntityMapper` for counter transactions and money orders/identity verification, where normalized lines, tax snapshots, payment rows, and masked identity columns differ from the domain aggregate shape. Do not place EF attributes or `DbContext` types in Domain.

## Alternatives considered

- Map every domain type directly: rejected for transaction graph persistence and the separate masked identity row.
- Duplicate every domain type into Infrastructure: rejected because it would add mapping code without a demonstrated storage/domain mismatch.
- Put SQL-shaped records in Application: rejected because their lifecycle belongs to Infrastructure.

## Consequences

Mapping code validates persisted transaction totals and status reconstruction, adding explicit code and failure points. Directly mapped entities need careful Fluent configuration and can remain coupled to the EF materializer conventions; future divergence should be justified rather than generalized.

## Implementation and verification

See `CounterTransactionRecord.cs`, `MoneyOrderRecord.cs`, `SqlEntityMapper.cs`, and the corresponding entity configurations. Mapping and round-trip assertions are in the SQL integration tests; schema and domain tests cover constraints and identity masking.
