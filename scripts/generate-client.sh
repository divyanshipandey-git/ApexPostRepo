#!/usr/bin/env bash
# Regenerates src/ApexPost.Client/ApexPost.Client.cs from contracts/v2.json with NSwag.
set -euo pipefail
cd "$(dirname "$0")/.."
command -v nswag >/dev/null || dotnet tool install --global NSwag.ConsoleCore
nswag run nswag.json
echo "Generated src/ApexPost.Client/ApexPost.Client.cs"
