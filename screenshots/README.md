# Screenshots

All images here were captured from the running project (API on `https://localhost:54402`, the Part A console harness and the generated-client consumer) on 2026-11-10. Request/response captures are real `curl -i` output; Swagger UI images are real renders of `/swagger`.

| Part | Files |
|---|---|
| A – offline store | `part-a-*.png` (menu, add scan, find, pending, sync, change-listener log, SQL++ queries, indexes) |
| C – OpenAPI / versioning / SDK | `part-c-swagger-v1.png` (deprecated, struck through), `part-c-swagger-v2.png`, `part-c-201-created.png` (Location header), `part-c-400-validation.png`, `part-c-409-conflict.png`, `part-c-404-notfound.png`, `part-c-console-consumer.png`, contracts, NSwag config |
| D – security | `part-d-401-*`, `part-d-403-*`, `part-d-200-valid-token.png` (idempotent re-upload: `duplicatesSkipped`), `part-d-partner-*`, `part-d-https-redirection.png`, `part-d-security-headers.png`, `part-d-openapi-security.png` |

## Still to capture by hand (need an interactive terminal / device)
Circuit breaker open/half-open/closed, airplane-mode sync, console login, Swagger "Authorize" dialog, SecureStorage/token-cleared. Record these in the demo video required by Part D.
