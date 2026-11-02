# ApexPost SQL demonstrations

The root `sql` folder contains runnable SQL examples and session-based demonstrations. The deployed schema, EF migration reference, reporting queries, and stored procedure definitions remain authoritative under [`database/`](../database/); the files there are intentionally not copied here.

## Reports

| Requirement | Canonical SQL |
|---|---|
| Daily branch sales, retail/service totals | [`queries/daily-branch-sales.sql`](queries/daily-branch-sales.sql) |
| Branches with no sales today | [`queries/branches-with-no-sales-today.sql`](queries/branches-with-no-sales-today.sql) |
| Top 10 products by revenue per region | [`queries/top-10-products-by-revenue-region.sql`](queries/top-10-products-by-revenue-region.sql) |
| Shipping volume | [`queries/shipping-volume.sql`](queries/shipping-volume.sql) |
| Money orders per branch | [`queries/money-orders-per-branch.sql`](queries/money-orders-per-branch.sql) |
| Low stock | [`queries/low-stock-report.sql`](queries/low-stock-report.sql) |
| Identity verifications per branch | [`queries/identity-verifications-per-branch.sql`](queries/identity-verifications-per-branch.sql) |

## Transactions and concurrency

Both transaction examples reject databases outside the `ApexPost_SqlIT_*` test-catalog prefix. The counter-sale example rolls back by default; change `@CommitDemo` only when intentionally testing against disposable data. The blocking scripts are read-only and use an explicit stock key.

- [`transactions/counter-sale-transaction.sql`](transactions/counter-sale-transaction.sql)
- [`transactions/stock-adjustment-transaction.sql`](transactions/stock-adjustment-transaction.sql)
- [`concurrency/`](concurrency/)

## Existing SQL assets

- Schema and idempotent deployment script: [`database/schema/`](../database/schema/)
- EF Core migrations: [`src/ApexPost.Infrastructure/Persistence/Migrations/`](../src/ApexPost.Infrastructure/Persistence/Migrations/)
- Stored procedures: [`database/stored-procedures/`](../database/stored-procedures/)
- Index evidence: [`database/indexes/`](../database/indexes/)
- Performance evidence: [`database/performance/`](../database/performance/) and [`PERFORMANCE.md`](../PERFORMANCE.md)
- Schema verification: [`database/verification/VerifyApexPostDb.sql`](../database/verification/VerifyApexPostDb.sql)

Run the `:r` wrapper scripts from the repository root in SQLCMD mode (or use `sqlcmd -i`) connected to a disposable development/test catalog. The two report-specific queries and transaction/concurrency demos are self-contained. Do not run transaction demos against persistent `ApexPostDb`. See [`database/README.md`](../database/README.md) for deployment and database setup guidance.

## Least-privilege application access

Use a dedicated database role for the Web application's runtime identity. These grants describe the intended permission set; no deployed restricted principal was provisioned or inspected as part of this repository verification.

| Permission | Granted objects | Why the runtime needs it |
|---|---|---|
| `SELECT` | Branches, counters, staff, staff credentials, roles, products/categories, branch stock, shipping zones/rates, shipments, counter transactions/lines/tax rates/payments, money orders, identity verifications, and stock movements | Sign-in/role checks, branch and counter authorization, current price/stock validation, transaction and auditor reports, and loading existing records before state changes. |
| `INSERT` | Branches, counters, staff, staff credentials, products, branch stock, counter transactions, transaction lines/tax rates, payments, shipments, money orders, identity verifications, stock movements, and audit entries | Supported Web workflows create these records; the stock-adjustment and transaction paths must insert their associated history, payment, and audit records. Grant only the subset actually used by enabled features. |
| `UPDATE` | Branches, counters, staff, products, branch stock, counter transactions, shipments, and money orders | The runtime supports branch/counter/staff/product maintenance, stock adjustment, transaction void/refund state changes, courier status updates, and money-order state changes. |
| `EXECUTE` | `dbo.usp_AdjustStock`, `dbo.usp_DailyBranchSales`, and `dbo.usp_LowStockReport` | The application calls these parameterized procedures for atomic stock changes and the daily-sales/low-stock report contracts. |

Do **not** grant `DELETE`, `db_owner`, `sysadmin`, `ALTER`, schema ownership, migration-history write access, or database-level security/backup permissions to the runtime identity. The runtime has no startup migration or schema-management workflow; apply migrations through a separate deployment identity with time-bounded schema-change permissions. Do not grant a permission merely because it appears in the sample below: remove any operation or table not required by the deployed feature set.

Use managed identity or a secret store for credentials; never put credentials in scripts or source control. For actual deployment verification, inspect effective access with `sys.database_permissions` and run a runtime-identity smoke test. These steps have not been performed against a deployed restricted identity.

```sql
CREATE ROLE [ApexPostRuntime];
-- Create/map the login or external user separately using the environment's identity mechanism.
ALTER ROLE [ApexPostRuntime] ADD MEMBER [ApexPostWebUser];

GRANT SELECT ON OBJECT::dbo.Branches TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.Counters TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.Staff TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.StaffCredentials TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.Roles TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.Products TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.ProductCategories TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.BranchStock TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.ShippingZones TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.ShippingRates TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.Shipments TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.CounterTransactions TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.TransactionLines TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.TransactionTaxRates TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.Payments TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.MoneyOrders TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.IdentityVerifications TO [ApexPostRuntime];
GRANT SELECT ON OBJECT::dbo.StockMovements TO [ApexPostRuntime];

GRANT INSERT, UPDATE ON OBJECT::dbo.Branches TO [ApexPostRuntime];
GRANT INSERT, UPDATE ON OBJECT::dbo.Counters TO [ApexPostRuntime];
GRANT INSERT, UPDATE ON OBJECT::dbo.Staff TO [ApexPostRuntime];
GRANT INSERT ON OBJECT::dbo.StaffCredentials TO [ApexPostRuntime];
GRANT INSERT, UPDATE ON OBJECT::dbo.Products TO [ApexPostRuntime];
GRANT INSERT, UPDATE ON OBJECT::dbo.BranchStock TO [ApexPostRuntime];
GRANT INSERT ON OBJECT::dbo.CounterTransactions TO [ApexPostRuntime];
GRANT UPDATE ON OBJECT::dbo.CounterTransactions TO [ApexPostRuntime];
GRANT INSERT ON OBJECT::dbo.TransactionLines TO [ApexPostRuntime];
GRANT INSERT ON OBJECT::dbo.TransactionTaxRates TO [ApexPostRuntime];
GRANT INSERT ON OBJECT::dbo.Payments TO [ApexPostRuntime];
GRANT INSERT, UPDATE ON OBJECT::dbo.Shipments TO [ApexPostRuntime];
GRANT INSERT, UPDATE ON OBJECT::dbo.MoneyOrders TO [ApexPostRuntime];
GRANT INSERT ON OBJECT::dbo.IdentityVerifications TO [ApexPostRuntime];
GRANT INSERT ON OBJECT::dbo.StockMovements TO [ApexPostRuntime];
GRANT INSERT ON OBJECT::dbo.AuditEntries TO [ApexPostRuntime];

GRANT EXECUTE ON OBJECT::dbo.usp_AdjustStock TO [ApexPostRuntime];
GRANT EXECUTE ON OBJECT::dbo.usp_DailyBranchSales TO [ApexPostRuntime];
GRANT EXECUTE ON OBJECT::dbo.usp_LowStockReport TO [ApexPostRuntime];
```

Confirm these grants against the deployed endpoints before applying them; remove any operation the selected Web features do not expose. Do not grant `DELETE`, migration metadata/import access, or schema-wide permissions unless a separately reviewed runtime feature requires them. A deployment identity should have separate, time-bounded schema permissions.
