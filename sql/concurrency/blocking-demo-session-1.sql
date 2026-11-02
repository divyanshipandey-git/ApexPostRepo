-- Session 1: take an update lock and hold it briefly. This script is read-only.
IF DB_NAME() NOT LIKE N'ApexPost_SqlIT[_]%'
    THROW 51120, 'Run this demonstration only in an ApexPost_SqlIT_* catalog.', 1;
GO

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

DECLARE @BranchCode varchar(10) = 'TEST01';
DECLARE @Sku varchar(20) = 'TESTSKU';

BEGIN TRANSACTION;
SELECT Quantity
FROM dbo.BranchStock WITH (UPDLOCK, HOLDLOCK)
WHERE BranchCode = @BranchCode
  AND Sku = @Sku;

-- Start Session 2 while this delay is running.
WAITFOR DELAY '00:00:25';
ROLLBACK TRANSACTION;
