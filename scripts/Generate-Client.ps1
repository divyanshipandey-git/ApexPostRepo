<#
.SYNOPSIS
  Regenerates src/ApexPost.Client/ApexPost.Client.cs from contracts/v2.json with NSwag.

.DESCRIPTION
  Run Export-Contracts.ps1 first so contracts/v2.json matches the running API.
#>
$ErrorActionPreference = "Stop"
Push-Location (Join-Path $PSScriptRoot "..")
try {
    dotnet tool restore 2>$null
    if ($LASTEXITCODE -ne 0) { dotnet tool install --global NSwag.ConsoleCore }
    nswag run nswag.json
    Write-Host "Generated src/ApexPost.Client/ApexPost.Client.cs"
}
finally { Pop-Location }
