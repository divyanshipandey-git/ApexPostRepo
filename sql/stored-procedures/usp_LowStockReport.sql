CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
    @BranchCode varchar(10) = NULL,
    @Threshold int = 5
AS
BEGIN
    SET NOCOUNT ON;

    -- Parameter validation
    IF @Threshold IS NULL OR @Threshold < 0 OR @Threshold > 100000
        THROW 51007, 'Threshold must be between 0 and 100000.', 1;

    IF @BranchCode IS NOT NULL AND NULLIF(LTRIM(RTRIM(@BranchCode)), '') IS NULL
        THROW 51008, 'Branch code cannot be blank.', 1;

    /*
    OPTION (RECOMPILE) Justification:
    
    This procedure has an optional @BranchCode parameter. When NULL, it returns
    low-stock items across all branches; when provided, it filters to one branch.
    
    The optimizer faces a parameter sniffing dilemma:
    - If first execution uses @BranchCode = NULL (all branches), it may choose
      a plan with a scan + hash match, optimal for large result sets.
    - If first execution uses a specific @BranchCode, it may choose a plan with
      an index seek on IX_BranchStock_Branch_Quantity_Sku, optimal for one branch.
    - Subsequent executions with the opposite parameter value suffer from the
      cached plan mismatch.
    
    OPTION (RECOMPILE) ensures:
    1. The plan is compiled for the actual parameter values at each execution
    2. The optimizer can use the filtered index efficiently when @BranchCode is provided
    3. No parameter sniffing issues across different branch filter scenarios
    
    Trade-off: Recompilation overhead on every execution.
    Mitigation: This report runs infrequently (on-demand by managers), so the
    CPU cost of recompilation is negligible compared to the benefit of optimal
    plans for both filtered and unfiltered cases.
    
    Alternative considered: Dynamic SQL with sp_executesql - rejected because
    it adds complexity and doesn't provide additional benefit over RECOMPILE
    for this simple parameter pattern.
    */

    SELECT stock.BranchCode, stock.Sku, product.Name AS ProductName,
           stock.Quantity, @Threshold AS Threshold
    FROM dbo.BranchStock AS stock
    INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
    WHERE stock.Quantity <= @Threshold
      AND (@BranchCode IS NULL OR stock.BranchCode = @BranchCode)
    ORDER BY stock.BranchCode, stock.Quantity, stock.Sku
    OPTION (RECOMPILE);
END;