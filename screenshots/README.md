# ApexPost Week 4 - Screenshots Documentation

This folder contains placeholder documentation for all required screenshots per the ApexPost Week 4 lab requirements.

## Structure

```
screenshots/
├── README.md                           # This file
├── part-a-screenshots.md               # Part A: Couchbase Lite offline persistence
├── part-c-screenshots.md               # Part C: Swagger/OpenAPI/SDK
└── part-d-screenshots.md               # Part D: API Security & Mobile Integration
```

---

## Screenshot Requirements by Part

### Part A: Couchbase Lite Offline Persistence (25 marks)
**Deliverable:** "Screenshots of the query results and the listener log" (line 64 of requirements)

Required screenshots:
1. **SQL++ Query Results** (3 queries):
   - All scans for a tracking number (ordered by scannedAtUtc)
   - All scans where syncStatus = 'Pending'
   - Number of scans per scanType for one courier
2. **Change Listener Log** - Console output showing live pending scans count
3. **Console Harness Menu** - All 6 menu operations (Add, Find, List Pending, Simulate Sync, Delete, Exit)
3. **Database Structure** - Couchbase Lite database with documents
4. **Value Indexes** - Indexes on trackingNumber and syncStatus

### Part C: Swagger/OpenAPI Integration (25 marks)
**Deliverable:** "Screenshots: Swagger UI showing v1 and v2, a 400 ValidationProblemDetails response, a 409 response, and a 201 response with its Location header" (line 208)

Required screenshots:
1. **Swagger UI** - Version dropdown (v1 deprecated, v2 current)
2. **Swagger UI v1** - All v1 endpoints
3. **Swagger UI v2** - All v2 endpoints with nested recipient object
4. **v1.json Contract** - Raw OpenAPI v1 document
4. **v2.json Contract** - Raw OpenAPI v2 document
5. **201 Created** - With Location header
6. **400 Bad Request** - ValidationProblemDetails
7. **404 Not Found** - ProblemDetails
8. **409 Conflict** - ProblemDetails
9. **Generated SDK** - nswag.json, client project, console consumer
10. **XML Documentation** - Comments, ProducesResponseType, Tags, EndpointSummary

### Part D: API Security & Mobile Integration (25 marks)
**Deliverable:** "A 3–5 minute demo video showing: Login. Scans saved while in airplane mode. Automatic sync after reconnecting. 401 and 403 responses. The circuit breaker opening while the API is stopped, then recovering." (lines 324-329)

Plus ".http file covering 401, 403, an invalid API key and a valid token" (line 323)

Required screenshots:
1. **JWT Auth** - 401 (missing/invalid/expired token), 403 (wrong role, courierId mismatch), 200 (valid)
2. **Partner API Keys** - 401 (missing/invalid), 200 (valid)
3. **HTTPS/HSTS** - Redirection, HSTS header, security headers, Swagger Authorize button
4. **Console App** - Login, New Scan (offline), Pending Scans, Airplane mode, Auto-sync, Sync Status
5. **Circuit Breaker** - Closed, Open (with banner), Half-Open, Recovery, Retry logs
6. **Secure Storage** - DPAPI token storage, token cleared on 401
7. **JSON Serialization** - Success deserialization, error ProblemDetails handling
8. **End-to-End Flow** - Complete offline-to-online flow
9. **Configuration** - appsettings (no secrets), User Secrets
10. **OpenAPI Security** - Bearer scheme, X-Api-Key scheme

---

## How to Capture Screenshots

### Part A (Couchbase Lite)
```bash
# Run the console app
cd src/ApexPost.Courier.Offline.Console
dotnet run

# Use menu options:
# 1. Add Scan (multiple)
# 2. Find by Tracking Number
# 3. List Pending
# 4. Simulate Sync
# 5. Delete Scan
# Capture console output for each
```

### Part C (Swagger UI)
```bash
# Run the API
cd src/ApexPost.Api
dotnet run

# Open browser: https://localhost:5001/swagger
# Select v1 and v2 from dropdown
# Use "Try it out" for each endpoint
# Capture 201, 400, 404, 409 responses
```

### Part D (Security & Console)
```bash
# Run API
cd src/ApexPost.Api
dotnet run

# Run Console App
cd src/ApexPost.Courier.Console
dotnet run

# Test 401/403/valid token scenarios
# Enable airplane mode (or disable network)
# Create scans offline
# Re-enable network, watch auto-sync
# Stop API, watch circuit breaker open
# Restart API, watch recovery
```

---

## File Naming Convention
```
screenshots/
├── part-a-query-tracking-number.png
├── part-a-query-pending.png
├── part-a-query-scan-counts.png
├── part-a-change-listener.png
├── part-a-console-menu.png
├── part-a-add-scan.png
├── part-a-find-by-tracking.png
├── part-a-list-pending.png
├── part-a-simulate-sync.png
├── part-a-delete-scan.png
├── part-a-database-structure.png
├── part-a-document-example.png
├── part-a-value-indexes.png
├── part-c-swagger-dropdown.png
├── part-c-swagger-v1.png
├── part-c-swagger-v2.png
├── part-c-v1-contract.png
├── part-c-v2-contract.png
├── part-c-201-created.png
├── part-c-400-validation.png
├── part-c-404-notfound.png
├── part-c-409-conflict.png
├── part-c-v1-endpoints.png
├── part-c-v2-endpoints.png
├── part-c-nswag-config.png
├── part-c-generated-client.png
├── part-c-console-consumer.png
├── part-c-xml-comments.png
├── part-c-produces-response-type.png
├── part-c-tags-endpoint-summary.png
├── part-d-401-missing-token.png
├── part-d-401-invalid-token.png
├── part-d-403-wrong-role.png
├── part-d-403-courier-mismatch.png
├── part-d-200-valid-token.png
├── part-d-partner-missing-key.png
├── part-d-partner-invalid-key.png
├── part-d-partner-valid-key.png
├── part-d-https-redirection.png
├── part-d-hsts-header.png
├── part-d-security-headers.png
├── part-d-swagger-authorize.png
├── part-d-console-login.png
├── part-d-console-new-scan-offline.png
├── part-d-console-pending-scans.png
├── part-d-console-airplane-mode.png
├── part-d-console-auto-sync.png
├── part-d-console-sync-status.png
├── part-d-circuit-closed.png
├── part-d-circuit-open.png
├── part-d-circuit-half-open.png
├── part-d-circuit-recovery.png
├── part-d-retry-logs.png
├── part-d-secure-storage.png
├── part-d-token-cleared.png
├── part-d-json-deserialize-success.png
├── part-d-json-deserialize-error.png
├── part-d-e2e-flow.png
├── part-d-appsettings-no-secrets.png
├── part-d-user-secrets.png
└── part-d-openapi-security.png
```

---

## Note
**Actual screenshots cannot be captured in this text-based coding environment.** These `.md` files document exactly what screenshots are required and what they should show. In a real development environment (Visual Studio/VS Code with running applications), you would:

1. Run each application
2. Navigate to the relevant screens
3. Take screenshots (Windows: Win+Shift+S, Mac: Cmd+Shift+4, Linux: Flameshot/GNOME Screenshot)
4. Save them in this `screenshots/` folder with the naming convention above
5. Commit them to the repository

The `ApexPost.Api.http` file in the root directory already contains all HTTP requests needed to reproduce the API responses shown in the screenshots.