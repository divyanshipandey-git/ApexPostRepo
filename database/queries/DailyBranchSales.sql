DECLARE @BranchCode varchar(10) = NULL;
DECLARE @Date date = CONVERT(date, SYSUTCDATETIME());

EXEC dbo.usp_DailyBranchSales
    @BranchCode = @BranchCode,
    @Date = @Date;
