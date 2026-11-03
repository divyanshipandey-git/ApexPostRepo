# ADR 0017: Keep report indexes tied to measured workloads

## Status

Accepted for the retained schema indexes; corrected synthetic LocalDB measurements are recorded, while production performance remains unverified.

## Context

The schema has low-stock, daily-sales, shipment, and auditor money-order workloads. An index can reduce reads but increases storage and write cost, so index selection must be linked to actual query shape and evidence.

## Decision

Retain the EF-owned indexes on branch/quantity/SKU, branch/occurrence/status, zone/service/created-time, and branch/created-time money orders. Do not add a competing SQL-only index definition. Keep the date filters half-open and SARGable. Revisit index key order only after measuring representative selectivity and write overhead with the corrected unhinted query comparison.

## Alternatives considered

- Add covering indexes for every projected column: rejected because it increases stock/sale write and storage costs without measured benefit.
- Apply index changes based only on plan appearance or assumed query frequency: rejected as insufficient evidence.
- Drop indexes based on the prior forced-index comparison: rejected because the comparison did not isolate the secondary indexes being evaluated.

## Consequences

The previously recorded LocalDB comparison forced primary-key indexes and therefore does not establish whether the retained secondary indexes help. The corrected setup and measurement scripts have since been executed against a disposable synthetic LocalDB catalog. [docs/WEEK3-COMPLETE-DOCUMENTATION.md#5-performance-evidence](../WEEK3-COMPLETE-DOCUMENTATION.md#5-performance-evidence) records those measured results, including the observed money-order seek benefit and the absence of observed benefit for the other tested read indexes. These measurements do not establish production performance or justify unmeasured index changes.

## Implementation and verification

Index definitions are in `ApexPostEntityConfigurations.cs` and EF migrations. Corrected measurement setup, captured output, and limitations are in [docs/WEEK3-COMPLETE-DOCUMENTATION.md#5-performance-evidence](../WEEK3-COMPLETE-DOCUMENTATION.md#5-performance-evidence), `database/performance/`, and `docs/performance-plans/actual-plans-and-statistics.txt`. The scripts are suitable for disposable measurement catalogs only. Run a separately controlled workload against representative SQL Server data before making production performance claims.
