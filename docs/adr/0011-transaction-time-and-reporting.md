# ADR 0011: UTC transaction occurrence time and repository-backed reports

## Status

Accepted

## Context

Daily branch sales require a stable occurrence time and report projections must reflect saved transaction, stock, shipment, and money-order data. Existing JSON files are user data and must remain readable without automatic rewriting.

## Decision

`CounterTransaction` stores an optional UTC `OccurredAtUtc`. New sales and demo seed transactions set it from `TimeProvider` or a fixed UTC seed date. Optionality preserves deserialization of earlier records. Reporting excludes records that lack a timestamp rather than assigning a synthetic date. `IReportDataSource` is implemented by `JsonReportDataSource`, which joins domain repositories into report DTOs. Dashboard sales and low-stock queries execute concurrently.

## Consequences

- New transactions participate in daily reporting with UTC date boundaries.
- Existing timestamp-free records remain unchanged and are not included in date-based reports.
- JSON repositories remain replaceable behind Application interfaces.
- Existing multi-file crash consistency limits continue to apply; reports reflect whichever persisted records are present.
