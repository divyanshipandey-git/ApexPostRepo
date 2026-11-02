-- Session 2: run while Session 1 is holding the update lock.
IF DB_NAME() NOT LIKE N'ApexPost_SqlIT[_]%'
    THROW 51121, 'Run this demonstration only in an ApexPost_SqlIT_* catalog.', 1;
GO

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

DECLARE @BranchCode varchar(10) = 'TEST01';
DECLARE @Sku varchar(20) = 'TESTSKU';

-- READCOMMITTEDLOCK makes the shared-lock wait explicit even when the database
-- has READ_COMMITTED_SNAPSHOT enabled.
SELECT Quantity
FROM dbo.BranchStock WITH (READCOMMITTEDLOCK)
WHERE BranchCode = @BranchCode
  AND Sku = @Sku;
