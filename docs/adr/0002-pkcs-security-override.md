# ADR 0002: Pin patched PKCS dependency for SQL Server client

## Status

Accepted

## Context

Adding the SQL Server EF Core provider introduced `Microsoft.Data.SqlClient` 6.1.7,
which resolves `System.Security.Cryptography.Pkcs` 9.0.11 transitively. NuGet audit
reported security advisories for that version, and the solution treats package audit
warnings as errors. The Week 3 package allowlist requires trainer approval for
packages outside its list.

## Decision

After trainer approval, pin `System.Security.Cryptography.Pkcs` to patched version
9.0.21 in the Infrastructure project. This is a transitive security override only;
it adds no application API usage and does not change runtime behavior beyond using
the patched dependency.

## Consequences

Restore resolves the patched package and retains the existing SQL Server provider.
Revisit the pin when the provider dependency graph no longer requires the override.
