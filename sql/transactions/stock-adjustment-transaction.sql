-- The procedure owns an explicit transaction. This outer test transaction
-- demonstrates that its nested COMMIT remains rollbackable by the caller.
IF DB_NAME() NOT LIKE N'ApexPost_SqlIT[_]%'
    THROW 51110, 'Run this demonstration only in an ApexPost_SqlIT_* catalog.', 1;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @BranchCode varchar(10) = 'TEST01';
DECLARE @Sku varchar(20) = 'TESTSKU';
DECLARE @Delta int = 1;
DECLARE @Reason nvarchar(500) = N'Rollback-only stock adjustment demonstration';
DECLARE @ChangedBy varchar(128) = 'TEST-CLERK';

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC dbo.usp_AdjustStock
        @BranchCode = @BranchCode,
        @Sku = @Sku,
        @Delta = @Delta,
        @Reason = @Reason,
        @ChangedBy = @ChangedBy;

    ROLLBACK TRANSACTION;
    PRINT 'The procedure ran successfully; the outer transaction rolled back the stock and movement changes.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
