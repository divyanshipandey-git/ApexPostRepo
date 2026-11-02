/*
   Run only against a new, isolated LocalDB catalog created for performance
   evidence. The script inserts fictional data and does not target ApexPostDb.
*/
IF DB_NAME() NOT LIKE N'ApexPost_PerfEvidence[_]%'
    THROW 51201, 'Run the synthetic setup only in an ApexPost_PerfEvidence_* catalog.', 1;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;

;WITH n AS (
    SELECT TOP (10) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects
)
INSERT dbo.Branches (BranchCode, Name, Region, OpeningTime, ClosingTime, CreatedBy)
SELECT CONCAT('P', RIGHT(CONCAT('00', n), 2)), CONCAT('Performance Branch ', n), 'Synthetic', '08:00', '20:00', 'performance-fixture'
FROM n;

;WITH n AS (
    SELECT TOP (10) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects
)
INSERT dbo.Counters (Id, BranchCode, Name, CreatedBy)
SELECT CONCAT('PERF-COUNTER-', n), CONCAT('P', RIGHT(CONCAT('00', n), 2)), 'Synthetic counter', 'performance-fixture'
FROM n;

;WITH n AS (
    SELECT TOP (10) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects
)
INSERT dbo.Staff (Id, Name, Email, Role, HomeBranchCode, CreatedBy)
SELECT CONCAT('PERF-STAFF-', n), 'Performance Staff', CONCAT('perf-staff-', n, '@example.test'), 'BranchManager', CONCAT('P', RIGHT(CONCAT('00', n), 2)), 'performance-fixture'
FROM n;

;WITH n AS (
    SELECT TOP (1000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT dbo.Products (Sku, Name, Category, UnitPrice, TaxCategory, CreatedBy)
SELECT CONCAT('PERF-', RIGHT(CONCAT('0000', n), 4)), CONCAT('Synthetic product ', n), 'Packaging', 10.00, 'Standard', 'performance-fixture'
FROM n;

INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedBy)
SELECT branch.BranchCode, product.Sku,
       CASE WHEN ABS(CHECKSUM(branch.BranchCode, product.Sku)) % 5 = 0 THEN 3 ELSE 25 END,
       'performance-fixture'
FROM dbo.Branches branch CROSS JOIN dbo.Products product
WHERE branch.BranchCode LIKE 'P%' AND product.Sku LIKE 'PERF-%';

INSERT dbo.Products (Sku, Name, Category, UnitPrice, TaxCategory, CreatedBy)
VALUES ('PERF-INSERT-ONLY', 'Synthetic insert-overhead product', 'Packaging', 10.00, 'Standard', 'performance-fixture');

;WITH n AS (
    SELECT TOP (50000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT dbo.CounterTransactions (Id, BranchCode, CounterId, ClerkId, Status, OccurredAtUtc, Subtotal, Tax, Total, CreatedBy)
SELECT CONCAT('PERF-TX-', n), CONCAT('P', RIGHT(CONCAT('00', ((n - 1) % 10) + 1), 2)),
       CONCAT('PERF-COUNTER-', ((n - 1) % 10) + 1), CONCAT('PERF-STAFF-', ((n - 1) % 10) + 1), 'Paid',
       DATEADD(minute, -n, SYSUTCDATETIME()), 100.00, 18.00, 118.00, 'performance-fixture'
FROM n;

INSERT dbo.TransactionLines (TransactionId, LineNumber, Description, ItemType, Quantity, UnitPrice, TaxCategory, ReferenceId, LineTotal, CreatedBy)
SELECT transactionRow.Id, 1, 'Synthetic retail sale', 'Retail', 1, 100.00, 'Standard',
       CONCAT('PERF-', RIGHT(CONCAT('0000', ABS(CHECKSUM(transactionRow.Id)) % 1000 + 1), 4)),
       100.00, 'performance-fixture'
FROM dbo.CounterTransactions AS transactionRow
WHERE transactionRow.Id LIKE 'PERF-TX-%';

INSERT dbo.TransactionTaxRates (TransactionId, Category, Rate, CreatedBy)
SELECT Id, 'Standard', 0.18, 'performance-fixture'
FROM dbo.CounterTransactions WHERE Id LIKE 'PERF-TX-%';

INSERT dbo.Payments (TransactionId, Method, Amount, PaidAtUtc, CreatedBy)
SELECT Id, 'Cash', 118.00, OccurredAtUtc, 'performance-fixture'
FROM dbo.CounterTransactions WHERE Id LIKE 'PERF-TX-%';

;WITH n AS (
    SELECT TOP (20000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT dbo.Shipments (Id, ItemType, WeightKg, LengthCm, WidthCm, HeightCm, ZoneCode, ServiceLevel, BranchCode, CreatedAtUtc, CreatedBy)
SELECT CONCAT('PERF-SHP-', n), 'Parcel', 1.0, 10, 10, 10, CONCAT('ZONE', ((n - 1) % 10) + 1),
       CASE n % 3 WHEN 0 THEN 'Registered' WHEN 1 THEN 'Standard' ELSE 'Express' END,
       CONCAT('P', RIGHT(CONCAT('00', ((n - 1) % 10) + 1), 2)), DATEADD(minute, -n, SYSUTCDATETIME()), 'performance-fixture'
FROM n;

;WITH n AS (
    SELECT TOP (20000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT dbo.MoneyOrders (Id, Amount, SenderName, RecipientName, Fee, BranchCode, CreatedAtUtc, CreatedBy)
SELECT CONCAT('PERF-MO-', n), 1000.00, 'Synthetic Sender', 'Synthetic Recipient', 10.00,
       CONCAT('P', RIGHT(CONCAT('00', ((n - 1) % 10) + 1), 2)), DATEADD(minute, -n, SYSUTCDATETIME()), 'performance-fixture'
FROM n;

;WITH numberedOrders AS
(
    SELECT Id, CreatedAtUtc,
           ROW_NUMBER() OVER (ORDER BY Id) AS RowNumber
    FROM dbo.MoneyOrders
    WHERE Id LIKE 'PERF-MO-%'
)
INSERT dbo.IdentityVerifications
    (Id, MoneyOrderId, DocumentType, MaskedDocumentNumber, IsVerified, VerifiedAtUtc, CreatedBy)
SELECT CONCAT('PERF-IDV-', RIGHT(CONCAT('00000', RowNumber), 5)),
       Id, 'Government ID', 'XXXX1234',
       CASE WHEN RowNumber % 2 = 0 THEN 1 ELSE 0 END,
       CreatedAtUtc, 'performance-fixture'
FROM numberedOrders;
