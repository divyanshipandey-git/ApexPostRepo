# ADR 0017: Keep report indexes tied to measured workloads

## Status

Accepted for the existing measured SQL fixture; re-evaluate with production-like data before adding indexes.

## Context

The schema has low-stock, daily-sales, shipment, and auditor money-order workloads. An index can reduce reads but increases storage and write cost, so index selection must be linked to actual query shape and evidence.

## Decision

Retain the EF-owned indexes on branch/quantity/SKU, branch/occurrence/status, zone/service/created-time, and branch/created-time money orders. Do not add a competing SQL-only index definition. Keep the date filters half-open and SARGable. Revisit index key order only after measuring representative selectivity and write overhead.

## Alternatives considered

- Add covering indexes for every projected column: rejected because it increases stock/sale write and storage costs without measured benefit.
- Apply index changes based only on plan appearance or assumed query frequency: rejected as insufficient evidence.
- Drop indexes that did not improve the documented synthetic run: rejected pending broader workload evidence.

## Consequences

The retained LocalDB measurements show no logical-read improvement for three of the four tested workloads; the money-order workload did reduce reads in the saved fixture output. Those are fixture-specific results, not production capacity claims.

## Implementation and verification

Index definitions are in `ApexPostEntityConfigurations.cs` and EF migrations. The exact observed measurements, limitations, and setup are in `PERFORMANCE.md`, `database/performance/`, and `database/indexes/VerifyReportIndexes.sql`. No new performance numbers are claimed by this ADR.
