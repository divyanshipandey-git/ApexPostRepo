DECLARE @BranchCode varchar(10) = NULL;
DECLARE @Threshold int = 5;

EXEC dbo.usp_LowStockReport
    @BranchCode = @BranchCode,
    @Threshold = @Threshold;
