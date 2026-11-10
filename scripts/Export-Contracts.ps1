<#
.SYNOPSIS
  Exports the OpenAPI documents of the RUNNING ApexPost API into contracts/.

.DESCRIPTION
  contracts/v1.json and contracts/v2.json must be the documents the API itself serves
  (built-in Microsoft.AspNetCore.OpenApi), not hand-edited files. Start the API in Development first:

    dotnet user-secrets set "Jwt:SigningKey" "<32+ chars>" --project src/ApexPost.Api
    dotnet user-secrets set "ConnectionStrings:ApexPost" "<connection string>" --project src/ApexPost.Api
    dotnet run --project src/ApexPost.Api --launch-profile https

  then run this script. Re-run it (and Generate-Client.ps1) after every API change.
#>
param(
    [string]$BaseUrl = "https://localhost:54402",
    [string]$OutDir  = (Join-Path $PSScriptRoot "..\contracts")
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

foreach ($v in "v1", "v2") {
    $target = Join-Path $OutDir "$v.json"
    Write-Host "Exporting $BaseUrl/openapi/$v.json -> $target"
    $response = Invoke-WebRequest -Uri "$BaseUrl/openapi/$v.json" -SkipCertificateCheck -UseBasicParsing
    # Re-serialise with stable indentation so diffs stay readable
    $response.Content | ConvertFrom-Json -Depth 100 | ConvertTo-Json -Depth 100 | Set-Content -Path $target -Encoding utf8
}

Write-Host "Done. Review 'git diff contracts/' and commit the result."
