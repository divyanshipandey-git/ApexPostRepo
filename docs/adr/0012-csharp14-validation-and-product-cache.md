# ADR 0012: C# 14 currency extension, cached validation metadata, and catalogue reads

## Status

Accepted

## Context

The assignment asks for C# 14 extension members, custom attribute validation with cached reflection metadata, and a `ValueTask` cache. These should solve actual repeated work or protect a business invariant.

## Decision

- `decimal.ToCurrencyPrecision()` is a C# 14 extension member used by `Money` to centralize two-place, midpoint-away-from-zero rounding.
- `InventoryStock.Quantity` uses the C# 14 `field` keyword in its private setter to centralize the non-negative invariant.
- Transaction request records use custom `RequiredTextAttribute` and `MaskedIdentifierAttribute` validation. `AttributeObjectValidator` caches property and attribute metadata per type and traverses nested request records before business orchestration.
- `ProductCatalogCache` uses a two-minute TTL for immutable product reads. Warm SKU hits return a completed `ValueTask<Product?>` synchronously; catalog reads warm the SKU cache before sale item resolution. An explicit invalidation API supports future catalogue writes.

## Consequences

- Currency rounding behavior is in one domain-level rule.
- Invalid nested request data is rejected before persistence, and metadata reflection occurs once per type.
- Warm catalog reads avoid JSON repository access, but concurrent misses are not coalesced, absent SKUs are not negatively cached, and stale data can persist until expiration or explicit invalidation.
- A local latency experiment is documented in `docs/performance-measurement.md`; it is illustrative and does not establish production throughput.
