<#
.SYNOPSIS
    Runs the performance evidence setup and measurements, saving actual execution plans as .sqlplan files.

.DESCRIPTION
    This script:
    1. Creates a temporary ApexPost_PerfEvidence_* database
    2. Runs the synthetic data setup (database/performance/performance-evidence-setup.sql)
    3. Runs the performance measurements (database/performance/performance-evidence-measurements.sql)
    4. Captures actual execution plans as .sqlplan files in docs/performance-plans/
    5. Cleans up the temporary database

.REQUIREMENTS
    - SQL Server LocalDB or SQL Server instance
    - sqlcmd utility
    - APEXPOST_PERF_CONNECTION environment variable set to a valid connection string
      (e.g., "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;")

.NOTES
    Run from repository root in PowerShell.
#>

param(
    [string]$ConnectionString = $env:APEXPOST_PERF_CONNECTION,
    [string]$RepositoryRoot = (Get-Location).Path
)

if (-not $ConnectionString) {
    Write-Error "APEXPOST_PERF_CONNECTION environment variable not set."
    Write-Error "Example: Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;"
    exit 1
}

$ErrorActionPreference = "Stop"

# Generate unique database name
$DbName = "ApexPost_PerfEvidence_$(Get-Random)"
$PerfConnectionString = (New-Object System.Data.SqlClient.SqlConnectionStringBuilder($ConnectionString)).ConnectionString
$Builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($ConnectionString)
$Builder.InitialCatalog = $DbName
$PerfConnectionString = $Builder.ConnectionString

Write-Host "Creating performance evidence database: $DbName" -ForegroundColor Cyan

# Create database
try {
    $CreateDbCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d master -E -Q `"CREATE DATABASE [$DbName];`""
    if ($Builder.IntegratedSecurity) {
        $CreateDbCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d master -E -Q `"CREATE DATABASE [$DbName];`""
    } else {
        $CreateDbCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d master -U $($Builder.UserID) -P $($Builder.Password) -Q `"CREATE DATABASE [$DbName];`""
    }
    Invoke-Expression $CreateDbCmd | Out-Null
} catch {
    Write-Error "Failed to create database: $_"
    exit 1
}

# Paths
$SetupScript = Join-Path $RepositoryRoot "database\performance\performance-evidence-setup.sql"
$MeasureScript = Join-Path $RepositoryRoot "database\performance\performance-evidence-measurements.sql"
$OutputDir = Join-Path $RepositoryRoot "docs\performance-plans"

# Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

Write-Host "Running performance evidence setup..." -ForegroundColor Cyan
$SetupCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d $DbName -E -i `"$SetupScript`""
if (-not $Builder.IntegratedSecurity) {
    $SetupCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d $DbName -U $($Builder.UserID) -P $($Builder.Password) -i `"$SetupScript`""
}
Invoke-Expression $SetupCmd | Tee-Object -Variable SetupOutput

Write-Host "Running performance measurements with plan capture..." -ForegroundColor Cyan

# Create a modified measurement script that outputs plans to files
$MeasureScriptWithPlans = @"
USE [$DbName];
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
SET STATISTICS XML ON;

DECLARE @StartUtc datetime2(7) = DATEADD(day, -30, SYSUTCDATETIME());
DECLARE @EndUtc datetime2(7) = SYSUTCDATETIME();

-- LOW-STOCK BEFORE
PRINT '=== LOW-STOCK BASELINE (no supporting index) ===';
SELECT stock.BranchCode, stock.Sku, product.Name, stock.Quantity
FROM dbo.BranchStock AS stock
JOIN dbo.Products AS product ON product.Sku = stock.Sku
WHERE stock.BranchCode = 'P01' AND stock.Quantity <= 5;
GO

CREATE INDEX IX_BranchStock_Branch_Quantity_Sku
    ON dbo.BranchStock (BranchCode, Quantity, Sku);
GO

PRINT '=== LOW-STOCK INDEXED (supporting index present) ===';
SELECT stock.BranchCode, stock.Sku, product.Name, stock.Quantity
FROM dbo.BranchStock AS stock
JOIN dbo.Products AS product ON product.Sku = stock.Sku
WHERE stock.BranchCode = 'P01' AND stock.Quantity <= 5;
GO

-- DAILY-SALES BEFORE
PRINT '=== DAILY-SALES BASELINE (no supporting index) ===';
SELECT COUNT_BIG(*) AS TransactionCount, SUM(transactionRow.Total) AS TotalSales
FROM dbo.CounterTransactions AS transactionRow
WHERE transactionRow.BranchCode = 'P01' AND transactionRow.Status = 'Paid'
  AND transactionRow.OccurredAtUtc >= @StartUtc
  AND transactionRow.OccurredAtUtc < @EndUtc;
GO

CREATE INDEX IX_CounterTransactions_Branch_Occurred_Status
    ON dbo.CounterTransactions (BranchCode, OccurredAtUtc, Status);
GO

PRINT '=== DAILY-SALES INDEXED (supporting index present) ===';
SELECT COUNT_BIG(*) AS TransactionCount, SUM(transactionRow.Total) AS TotalSales
FROM dbo.CounterTransactions AS transactionRow
WHERE transactionRow.BranchCode = 'P01' AND transactionRow.Status = 'Paid'
  AND transactionRow.OccurredAtUtc >= @StartUtc
  AND transactionRow.OccurredAtUtc < @EndUtc;
GO

-- SHIPMENT BEFORE
PRINT '=== SHIPMENT BASELINE (no supporting index) ===';
SELECT Id, BranchCode, ZoneCode, ServiceLevel, CreatedAtUtc
FROM dbo.Shipments
WHERE ZoneCode = 'ZONE1' AND ServiceLevel = 'Standard'
  AND CreatedAtUtc >= @StartUtc AND CreatedAtUtc < @EndUtc;
GO

CREATE INDEX IX_Shipments_Zone_Service_Created
    ON dbo.Shipments (ZoneCode, ServiceLevel, CreatedAtUtc);
GO

PRINT '=== SHIPMENT INDEXED (supporting index present) ===';
SELECT Id, BranchCode, ZoneCode, ServiceLevel, CreatedAtUtc
FROM dbo.Shipments
WHERE ZoneCode = 'ZONE1' AND ServiceLevel = 'Standard'
  AND CreatedAtUtc >= @StartUtc AND CreatedAtUtc < @EndUtc;
GO

-- MONEY-ORDER BEFORE
PRINT '=== MONEY-ORDER BASELINE (no supporting index) ===';
SELECT Id, BranchCode, Amount, Fee, TransactionId, CreatedAtUtc
FROM dbo.MoneyOrders
WHERE BranchCode = 'P01' AND CreatedAtUtc >= @StartUtc AND CreatedAtUtc < @EndUtc
ORDER BY CreatedAtUtc DESC;
GO

CREATE INDEX IX_MoneyOrders_Branch_Created
    ON dbo.MoneyOrders (BranchCode, CreatedAtUtc, Id)
    INCLUDE (Amount, Fee, TransactionId);
GO

PRINT '=== MONEY-ORDER INDEXED (supporting index present) ===';
SELECT Id, BranchCode, Amount, Fee, TransactionId, CreatedAtUtc
FROM dbo.MoneyOrders
WHERE BranchCode = 'P01' AND CreatedAtUtc >= @StartUtc AND CreatedAtUtc < @EndUtc
ORDER BY CreatedAtUtc DESC;
GO

PRINT '=== INDEX STORAGE ===';
SELECT OBJECT_NAME(partitionStats.object_id) AS TableName,
       indexInfo.name AS IndexName,
       SUM(partitionStats.row_count) AS TotalRows,
       SUM(partitionStats.reserved_page_count) * 8 AS ReservedKB,
       SUM(partitionStats.used_page_count) * 8 AS UsedKB
FROM sys.dm_db_partition_stats AS partitionStats
INNER JOIN sys.indexes AS indexInfo
    ON indexInfo.object_id = partitionStats.object_id
   AND indexInfo.index_id = partitionStats.index_id
WHERE OBJECT_NAME(partitionStats.object_id) IN
    ('BranchStock', 'CounterTransactions', 'Shipments', 'MoneyOrders')
GROUP BY partitionStats.object_id, indexInfo.name
ORDER BY TableName, IndexName;
GO

PRINT '=== NON-SARGABLE DATE PREDICATE ===';
SELECT COUNT_BIG(*) AS TransactionCount
FROM dbo.CounterTransactions
WHERE BranchCode = 'P01' AND YEAR(OccurredAtUtc) = YEAR(SYSUTCDATETIME());
GO

PRINT '=== SARGABLE DATE-RANGE PREDICATE ===';
SELECT COUNT_BIG(*) AS TransactionCount
FROM dbo.CounterTransactions
WHERE BranchCode = 'P01'
  AND OccurredAtUtc >= DATEFROMPARTS(YEAR(SYSUTCDATETIME()), 1, 1)
  AND OccurredAtUtc < DATEFROMPARTS(YEAR(SYSUTCDATETIME()) + 1, 1, 1);
GO
"@

$TempMeasureScript = Join-Path $env:TEMP "performance-evidence-measurements-with-plans.sql"
$MeasureScriptWithPlans | Out-File -FilePath $TempMeasureScript -Encoding utf8

# Run measurement and capture output including XML plans
$MeasureCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d $DbName -E -i `"$TempMeasureScript`" -o `"$OutputDir\performance-raw-output.txt`""
if (-not $Builder.IntegratedSecurity) {
    $MeasureCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d $DbName -U $($Builder.UserID) -P $($Builder.Password) -i `"$TempMeasureScript`" -o `"$OutputDir\performance-raw-output.txt`""
}
Invoke-Expression $MeasureCmd

# Parse the output to extract XML plans and save as .sqlplan files
$RawOutput = Get-Content -Path "$OutputDir\performance-raw-output.txt" -Raw

# Split by GO batches and extract XML plans
$Batches = $RawOutput -split '(?m)^GO\s*$'
$PlanIndex = 0

foreach ($Batch in $Batches) {
    # Look for ShowPlanXML in the batch
    if ($Batch -match '(<ShowPlanXML[\s\S]*?</ShowPlanXML>)') {
        $PlanXml = $matches[1]
        $PlanIndex++
        $PlanFile = Join-Path $OutputDir "plan_$PlanIndex.sqlplan"
        $PlanXml | Out-File -FilePath $PlanFile -Encoding utf8
        Write-Host "Saved execution plan to: $PlanFile" -ForegroundColor Green
    }
}

# Also extract STATISTICS IO/TIME output for analysis
$StatsOutput = $RawOutput -replace '(?s)<ShowPlanXML[\s\S]*?</ShowPlanXML>', '[EXECUTION PLAN REMOVED]'
$StatsOutput | Out-File -FilePath "$OutputDir\performance-statistics.txt" -Encoding utf8

Write-Host "Performance evidence complete. Output in: $OutputDir" -ForegroundColor Cyan
Write-Host "Files created:" -ForegroundColor Cyan
Get-ChildItem -Path $OutputDir | ForEach-Object { Write-Host "  $($_.Name)" }

# Cleanup
Write-Host "Dropping database $DbName..." -ForegroundColor Yellow
$DropCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d master -E -Q `"IF DB_ID('$DbName') IS NOT NULL BEGIN ALTER DATABASE [$DbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DbName]; END`""
if (-not $Builder.IntegratedSecurity) {
    $DropCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d master -U $($Builder.UserID) -P $($Builder.Password) -Q `"IF DB_ID('$DbName') IS NOT NULL BEGIN ALTER DATABASE [$DbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DbName]; END`""
}
Invoke-Expression $DropCmd | Out-Null

Remove-Item -Path $TempMeasureScript -Force -ErrorAction SilentlyContinue

Write-Host "Done." -ForegroundColor Green