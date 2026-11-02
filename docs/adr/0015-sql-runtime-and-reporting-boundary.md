# ADR 0015: SQL Server as the Web runtime store

## Status

Accepted for the current Week 3 implementation.

## Context

The Web application must have one business-data source after the JSON import. Repository ports are already defined in Application, and the existing Infrastructure project owns JSON adapters and can also own EF Core mappings without changing the dependency direction.

## Decision

Web composition registers scoped EF Core SQL Server repositories, `IAuditStore`, `IReportDataSource`, and `IUnitOfWork` against one scoped `ApexPostDbContext`. Web does not register JSON repositories and does not fall back to JSON when SQL is unavailable. Development connection strings are supplied through user-secrets; migrations remain an explicit operator action.

`usp_AdjustStock` is a stored procedure because stock quantity and its durable movement row must be validated and changed atomically, with locking, negative-stock protection, and a single transactional error boundary. The procedure call uses EF parameter interpolation.

`usp_DailyBranchSales` and `usp_LowStockReport` are parameterized database procedures used by `SqlReportDataSource`. Daily sales returns separate retail, service, and overall totals for a selected UTC date and optional branch. Low stock accepts a validated branch and threshold. Shipping volume and money-order/identity-verification summaries are filtered and grouped in SQL, not loaded in full and filtered in memory. Report DTOs remain provider-neutral.

## Consequences

Runtime SQL registration, SQL repository workflows, and SQL-backed MVC workflows are covered by opt-in LocalDB tests using isolated `ApexPost_SqlIT_*` catalogs. The role-authorization integration test logs in as each supported role and verifies report, inventory, counter-sale, stock, and branch-scope permissions. New filtered report and stored-procedure rollback assertions are also opt-in; they were not run in this environment because `APEXPOST_SQL_TEST_CONNECTION` is not configured. Existing Week 2 behavior tests continue to exercise JSON adapters in isolated test directories.
