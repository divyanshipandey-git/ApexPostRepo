-- List active branches without a paid counter sale today (UTC).
-- Transaction predicates are in the JOIN to preserve branches with no match.
DECLARE @TodayUtc date = CONVERT(date, SYSUTCDATETIME());
DECLARE @StartUtc datetime2(7) = CONVERT(datetime2(7), @TodayUtc);
DECLARE @EndUtc datetime2(7) = DATEADD(day, 1, @StartUtc);

SELECT branch.BranchCode,
       branch.Name,
       branch.Region
FROM dbo.Branches AS branch
LEFT JOIN dbo.CounterTransactions AS transactionRow
    ON transactionRow.BranchCode = branch.BranchCode
   AND transactionRow.Status = 'Paid'
   AND transactionRow.OccurredAtUtc >= @StartUtc
   AND transactionRow.OccurredAtUtc < @EndUtc
WHERE branch.IsActive = 1
  AND transactionRow.Id IS NULL
ORDER BY branch.Region, branch.BranchCode;
