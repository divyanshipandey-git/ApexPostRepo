# ADR 0016: Normalize the SQL schema and restrict historical deletes

## Status

Accepted for the current SQL Server model.

## Context

The SQL store must preserve branch, inventory, transaction, payment, shipment, money-order, audit, and reference-data integrity. Deleting a parent record must not silently erase financial or audit history.

## Decision

Keep the current normalized tables and keys: branch-owned records reference `BranchCode`; products use `Sku`; stock uses `(BranchCode, Sku)`; transaction lines, tax snapshots, and payments are separate rows; identity verification is stored separately from its money order. Role, category, zone, and rate data use lookup/reference tables where the model already defines them. EF configurations name the important primary/foreign/check/unique constraints and use `NO ACTION` for historical relationships rather than cascading deletes. Stock/transaction rowversions protect concurrent changes. Identity numbers are stored only as the constrained `XXXX` plus four-character masked form.

## Alternatives considered

- Cascade-delete financial records with their branch or transaction: rejected because history must remain referentially valid.
- Store all transaction data in one denormalized JSON/blob column: rejected because line, payment, and reporting workloads need relational constraints and projections.
- Store the full identity-document number: rejected for privacy and because the application contract does not require it.

## Consequences

Application workflows must explicitly manage linked records and cannot physically delete historical parents with dependent rows. The design prevents update/delete anomalies across shared category, zone, rate, and transaction data at the cost of more joins and explicit mappings.

## Implementation and verification

See `ApexPostEntityConfigurations.cs`, the initial migration, `MoneyOrderRecord.cs`, `SqlEntityMapper.cs`, and `database/schema/ApexPostDb-idempotent.sql`. Relevant verification is in `SqlServerIntegrationTests.Migrations_CreateTheExpectedSchemaOnSqlServer`, identity-mask domain tests, and `SqlWebApplicationIntegrationTests`; SQL tests require the isolated LocalDB setting.
