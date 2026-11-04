<#
.SYNOPSIS
    Runs the two-session blocking demonstration and saves output including sys.dm_exec_requests.

.DESCRIPTION
    This script:
    1. Creates a temporary ApexPost_SqlIT_* database
    2. Seeds it with test data (BranchStock row)
    3. Runs Session 1 script (blocking-demo-session-1.sql) in background
    4. Waits briefly, then runs Session 2 script (blocking-demo-session-2.sql)
    5. Captures sys.dm_exec_requests output showing blocking
    6. Saves all output to docs/performance-plans/blocking-demo-output.txt
    7. Cleans up the temporary database

.REQUIREMENTS
    - SQL Server LocalDB or SQL Server instance
    - sqlcmd utility
    - APEXPOST_SQL_TEST_CONNECTION environment variable set to a valid connection string
      (e.g., "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;")

.NOTES
    Run from repository root in PowerShell.
    Requires two separate sqlcmd sessions to demonstrate blocking.
#>

param(
    [string]$ConnectionString = $env:APEXPOST_SQL_TEST_CONNECTION,
    [string]$RepositoryRoot = (Get-Location).Path
)

if (-not $ConnectionString) {
    Write-Error "APEXPOST_SQL_TEST_CONNECTION environment variable not set."
    Write-Error "Example: Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;"
    exit 1
}

$ErrorActionPreference = "Stop"

# Generate unique database name
$DbName = "ApexPost_SqlIT_Blocking_$(Get-Random)"
$Builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($ConnectionString)
$Builder.InitialCatalog = $DbName
$TestConnectionString = $Builder.ConnectionString

Write-Host "Creating blocking demo database: $DbName" -ForegroundColor Cyan

# Create database
try {
    if ($Builder.IntegratedSecurity) {
        $CreateCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d master -E -Q `"CREATE DATABASE [$DbName];`""
    } else {
        $CreateCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d master -U $($Builder.UserID) -P $($Builder.Password) -Q `"CREATE DATABASE [$DbName];`""
    }
    Invoke-Expression $CreateCmd | Out-Null
} catch {
    Write-Error "Failed to create database: $_"
    exit 1
}

# Apply migrations to create schema
Write-Host "Applying schema..." -ForegroundColor Cyan
$MigrationCmd = "dotnet ef database update --project src/ApexPost.Infrastructure --startup-project src/ApexPost.Web --connection `"$TestConnectionString`""
Invoke-Expression $MigrationCmd | Out-Null

# Seed test data for blocking demo
Write-Host "Seeding test data..." -ForegroundColor Cyan
$SeedScript = @"
USE [$DbName];
GO
SET NOCOUNT ON;
INSERT dbo.Branches (BranchCode, Name, Region, OpeningTime, ClosingTime, CreatedBy)
VALUES ('TEST01', 'Test Branch', 'Test', '08:00', '20:00', 'blocking-demo');
INSERT dbo.Products (Sku, Name, Category, UnitPrice, TaxCategory, CreatedBy)
VALUES ('TESTSKU', 'Test Product', 'Test', 10.00, 'Standard', 'blocking-demo');
INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedBy)
VALUES ('TEST01', 'TESTSKU', 100, 'blocking-demo');
GO
"@

$SeedFile = Join-Path $env:TEMP "blocking-seed.sql"
$SeedScript | Out-File -FilePath $SeedFile -Encoding utf8

$SeedCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d $DbName -E -i `"$SeedFile`""
if (-not $Builder.IntegratedSecurity) {
    $SeedCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d $DbName -U $($Builder.UserID) -P $($Builder.Password) -i `"$SeedFile`""
}
Invoke-Expression $SeedCmd | Out-Null

# Paths
$Session1Script = Join-Path $RepositoryRoot "sql\concurrency\blocking-demo-session-1.sql"
$Session2Script = Join-Path $RepositoryRoot "sql\concurrency\blocking-demo-session-2.sql"
$OutputDir = Join-Path $RepositoryRoot "docs\performance-plans"
$OutputFile = Join-Path $OutputDir "blocking-demo-output.txt"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

Write-Host "Starting Session 1 (holds UPDLOCK for 25 seconds)..." -ForegroundColor Cyan

# Start Session 1 in background job
$Session1Cmd = "sqlcmd -S `"$($Builder.DataSource)`" -d $DbName -E -i `"$Session1Script`""
if (-not $Builder.IntegratedSecurity) {
    $Session1Cmd = "sqlcmd -S `"$($Builder.DataSource)`" -d $DbName -U $($Builder.UserID) -P $($Builder.Password) -i `"$Session1Script`""
}

$Job1 = Start-Job -ScriptBlock {
    param($Cmd)
    Invoke-Expression $Cmd
} -ArgumentList $Session1Cmd

# Give Session 1 time to acquire the lock
Start-Sleep -Seconds 3

Write-Host "Capturing sys.dm_exec_requests during blocking..." -ForegroundColor Cyan

# Capture blocking info from a third session
$BlockingQuery = @"
USE [$DbName];
GO
SELECT 
    r.session_id,
    r.blocking_session_id,
    r.wait_type,
    r.wait_time,
    r.wait_resource,
    r.status,
    r.command,
    SUBSTRING(st.text, (r.statement_start_offset/2)+1,
        ((CASE r.statement_end_offset WHEN -1 THEN DATALENGTH(st.text)
        ELSE r.statement_end_offset END - r.statement_start_offset)/2)+1) AS statement_text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) st
WHERE r.blocking_session_id <> 0
   OR r.session_id IN (SELECT blocking_session_id FROM sys.dm_exec_requests WHERE blocking_session_id <> 0);
GO
"@

$BlockingFile = Join-Path $env:TEMP "blocking-query.sql"
$BlockingQuery | Out-File -FilePath $BlockingFile -Encoding utf8

$BlockingCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d $DbName -E -i `"$BlockingFile`""
if (-not $Builder.IntegratedSecurity) {
    $BlockingCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d $DbName -U $($Builder.UserID) -P $($Builder.Password) -i `"$BlockingFile`""
}

$BlockingOutput = Invoke-Expression $BlockingCmd

Write-Host "Running Session 2 (waits for shared lock)..." -ForegroundColor Cyan

# Run Session 2
$Session2Cmd = "sqlcmd -S `"$($Builder.DataSource)`" -d $DbName -E -i `"$Session2Script`""
if (-not $Builder.IntegratedSecurity) {
    $Session2Cmd = "sqlcmd -S `"$($Builder.DataSource)`" -d $DbName -U $($Builder.UserID) -P $($Builder.Password) -i `"$Session2Script`""
}

$Session2Output = Invoke-Expression $Session2Cmd

# Wait for Session 1 to complete
$Job1 | Wait-Job | Receive-Job

$Session1Output = $Job1 | Receive-Job
Remove-Job $Job1 -Force

# Compile output
$FullOutput = @"
BLOCKING DEMONSTRATION OUTPUT
=============================
Database: $DbName
Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

--- SESSION 1 OUTPUT (holds UPDLOCK) ---
$Session1Output

--- SESSION 2 OUTPUT (waits for shared lock) ---
$Session2Output

--- SYS.DM_EXEC_REQUESTS DURING BLOCKING ---
$BlockingOutput
"@

$FullOutput | Out-File -FilePath $OutputFile -Encoding utf8

Write-Host "Blocking demo output saved to: $OutputFile" -ForegroundColor Green

# Cleanup
Write-Host "Dropping database $DbName..." -ForegroundColor Yellow
$DropCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d master -E -Q `"IF DB_ID('$DbName') IS NOT NULL BEGIN ALTER DATABASE [$DbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DbName]; END`""
if (-not $Builder.IntegratedSecurity) {
    $DropCmd = "sqlcmd -S `"$($Builder.DataSource)`" -d master -U $($Builder.UserID) -P $($Builder.Password) -Q `"IF DB_ID('$DbName') IS NOT NULL BEGIN ALTER DATABASE [$DbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DbName]; END`""
}
Invoke-Expression $DropCmd | Out-Null

Remove-Item -Path $SeedFile -Force -ErrorAction SilentlyContinue
Remove-Item -Path $BlockingFile -Force -ErrorAction SilentlyContinue

Write-Host "Done." -ForegroundColor Green