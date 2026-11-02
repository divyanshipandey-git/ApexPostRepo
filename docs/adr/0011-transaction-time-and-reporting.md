# ADR 0011: UTC transaction occurrence time and repository-backed reports

## Status

Accepted; the JSON compatibility statements below apply to the legacy adapters, while the current Web runtime uses SQL as documented in [ADR-0015](0015-sql-runtime-and-reporting-boundary.md).

## Context

Daily branch sales require a stable occurrence time and report projections must reflect saved transaction, stock, shipment, and money-order data. Existing JSON files are user data and must remain readable without automatic rewriting.

## Decision

`CounterTransaction` stores an optional UTC `OccurredAtUtc`. New sales and demo seed transactions set it from `TimeProvider` or a fixed UTC seed date. Optionality preserves deserialization of earlier records. Reporting excludes records that lack a timestamp rather than assigning a synthetic date. The Web `SqlReportDataSource` uses parameterized SQL for daily sales, the low-stock procedure for threshold queries, and SQL-side date/branch filtering and grouping for shipping and auditor aggregates. `JsonReportDataSource` remains only a legacy/test adapter. Dashboard sales and low-stock queries execute concurrently only in adapters that can safely do so; the scoped SQL context does not run concurrent operations.

## Consequences

- New transactions participate in daily reporting with UTC date boundaries.
- Existing timestamp-free records remain unchanged and are not included in date-based reports.
- Report screens use SQL projections and stored procedures, return no sensitive identity document fields, and display an explicit empty-state message when no rows match.
- The MVC report supports a UTC date, optional branch selection for Auditor/Administrator, Branch Manager home-branch scoping, and a validated threshold.
