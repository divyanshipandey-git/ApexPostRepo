# Least-Privilege Database Permissions for ApexPost Application Login

## Principle
The application login should have only the permissions necessary to perform its operations. No `db_owner`, `db_ddladmin`, or `sysadmin` roles.

## Required Database Role: `apexpost_app`

Create a custom database role and grant only the specific permissions needed:

```sql
-- Run once by DBA
USE ApexPostDb;
CREATE ROLE apexpost_app;

-- Table permissions: SELECT, INSERT, UPDATE, DELETE on business tables
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.Branches TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.Counters TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.Staff TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.StaffCredentials TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.Products TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.BranchStock TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.CounterTransactions TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.TransactionLines TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.TransactionTaxRates TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.Payments TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.Shipments TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.MoneyOrders TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.IdentityVerifications TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.StockMovements TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.AuditEntries TO apexpost_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.JsonImportRuns TO apexpost_app;

-- Read-only on reference/lookup tables
GRANT SELECT ON dbo.Roles TO apexpost_app;
GRANT SELECT ON dbo.ProductCategories TO apexpost_app;
GRANT SELECT ON dbo.ShippingZones TO apexpost_app;
GRANT SELECT ON dbo.ShippingRates TO apexpost_app;

-- Execute permissions on stored procedures (EXPLICITLY GRANTED)
GRANT EXECUTE ON dbo.usp_DailyBranchSales TO apexpost_app;
GRANT EXECUTE ON dbo.usp_LowStockReport TO apexpost_app;
GRANT EXECUTE ON dbo.usp_AdjustStock TO apexpost_app;

-- No permissions on:
-- - __EFMigrationsHistory (migrations applied by CI/CD, not app)
-- - DDL operations (CREATE, ALTER, DROP)
-- - Security administration
-- - Backup/restore
-- - Server-level permissions

-- Create login and user (run by DBA)
-- CREATE LOGIN apexpost_app WITH PASSWORD = 'StrongRandomPassword123!';
-- CREATE USER apexpost_app FOR LOGIN apexpost_app;
-- ALTER ROLE apexpost_app ADD MEMBER apexpost_app;
```

## Connection String (Development - User Secrets)
```
Server=(localdb)\MSSQLLocalDB;Database=ApexPostDb;User Id=apexpost_app;Password=StrongRandomPassword123!;TrustServerCertificate=True;
```

## Production Considerations
- Use Azure AD authentication or contained database users where possible
- Rotate passwords regularly
- Audit login attempts
- Consider row-level security for multi-tenant scenarios (not needed for ApexPost)
- Monitor for permission escalation attempts

## EF Core Migrations
Migrations are applied by the CI/CD pipeline using a separate elevated identity, NOT the application login. The application login must NOT have `db_ddladmin` or permission to modify schema.