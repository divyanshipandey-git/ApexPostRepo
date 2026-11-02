DECLARE @StartUtc datetime2(7) = DATEADD(day, -30, SYSUTCDATETIME());
DECLARE @EndUtc datetime2(7) = SYSUTCDATETIME();
DECLARE @BranchCode varchar(10) = NULL;

SELECT orders.BranchCode,
       COUNT_BIG(*) AS IdentityVerificationCount
FROM dbo.IdentityVerifications AS verification
INNER JOIN dbo.MoneyOrders AS orders ON orders.Id = verification.MoneyOrderId
WHERE verification.VerifiedAtUtc >= @StartUtc
  AND verification.VerifiedAtUtc < @EndUtc
  AND (@BranchCode IS NULL OR orders.BranchCode = @BranchCode)
GROUP BY orders.BranchCode
ORDER BY orders.BranchCode;
