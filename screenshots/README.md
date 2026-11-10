# Screenshots

All images here were captured from the running project (API on `https://localhost:54402`, the Part A console harness and the generated-client consumer) on 2026-11-10. Request/response captures are real `curl -i` output; Swagger UI images are real renders of `/swagger`.

| Part | Files |
|---|---|
| A – offline store | `part-a-*.png` (menu, add scan, find, pending, sync, change-listener log, SQL++ queries, indexes) |
| C – OpenAPI / versioning / SDK | `part-c-swagger-v1.png` (deprecated, struck through), `part-c-swagger-v2.png`, `part-c-201-created.png` (Location header), `part-c-400-validation.png`, `part-c-409-conflict.png`, `part-c-404-notfound.png`, `part-c-console-consumer.png`, contracts, NSwag config |
| D – security | `part-d-401-*`, `part-d-403-*`, `part-d-200-valid-token.png` (idempotent re-upload: `duplicatesSkipped`), `part-d-partner-*`, `part-d-https-redirection.png`, `part-d-security-headers.png`, `part-d-openapi-security.png` |

Also: `part-d-swagger-authorize.png` – the Swagger UI **Authorize** dialog with a development Bearer token entered (value masked) and the partner ApiKey scheme.

The courier client is a console app (agreed stand-in for MAUI), so there is no MAUI/device capture and no demo video.

## Added in the final pass (real runtime captures)
- `part-d-retry-logs`, `part-d-circuit-open`, `part-d-circuit-half-open`, `part-d-circuit-recovery`, `part-d-circuit-closed`, `part-d-token-cleared`, `part-d-console-*`, `part-d-e2e-flow`: output of `ApexPost.Courier.Console` against the live API (API stopped, then restarted).
- `part-a-change-listener`: pending count updating after each add next to the `[Listener]` log lines.
- `part-c-try-it-out`: Swagger UI "Try it out" executing `GET /api/v2/shipments` (200). The bearer token is replaced by a placeholder in the image.
- `part-d-secure-storage`, `part-d-json-deserialize-*`: the relevant source (token store behind `ITokenStorage`, source-generated JSON context and ProblemDetails handling).
