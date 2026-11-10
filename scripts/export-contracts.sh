#!/usr/bin/env bash
# Exports the OpenAPI documents of the RUNNING ApexPost API into contracts/.
# See Export-Contracts.ps1 for the prerequisites (secrets + `dotnet run --project src/ApexPost.Api`).
set -euo pipefail
BASE_URL="${1:-https://localhost:54402}"
OUT_DIR="$(cd "$(dirname "$0")/.." && pwd)/contracts"
mkdir -p "$OUT_DIR"
for v in v1 v2; do
  echo "Exporting $BASE_URL/openapi/$v.json -> $OUT_DIR/$v.json"
  curl --fail --silent --show-error --insecure "$BASE_URL/openapi/$v.json" | python3 -m json.tool > "$OUT_DIR/$v.json"
done
echo "Done. Review 'git diff contracts/' and commit the result."
