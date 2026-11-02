# ADR 0026: Keep Address outside the SQL persistence model

## Status

Accepted.

## Context

The Domain defines `Address` as an immutable value object, but no persisted entity or current Web workflow has an `Address` property. Shipping destinations are represented by a `ShippingZone` reference, and branch, counter, staff, and transaction records do not currently collect postal addresses. Adding an owned mapping would create unused columns and a schema migration without a persisted business requirement.

## Decision

Do not map `Address` as an EF owned/value object in the current SQL model. Keep the type available to domain use cases that need it, and add persistence only when a specific persisted entity owns an address and its required fields are defined.

## Consequences

No address columns or migration are required for the current model. If a future feature begins storing addresses, it must add an explicit owning entity/property, define nullability and validation, add an EF configuration and migration, and test round-trip persistence.

## Implementation and verification

`Address` is defined in `src/ApexPost.Domain/ValueObjects/Address.cs`. The persisted model is configured in `src/ApexPost.Infrastructure/Persistence/ApexPostEntityConfigurations.cs`; no mapped entity currently references `Address`. This decision is scoped to the current workflows and does not claim that addresses are never needed.
