# Screenshots - Part D: API Security & Mobile Integration

## 1. API Security Screenshots

### 1.1 JWT Bearer Authentication - 401 Unauthorized (Missing Token)
**File:** `screenshots/part-d-401-missing-token.png`
**Description:** Swagger UI or HTTP client showing:
- Request without Authorization header
- Response: 401 Unauthorized
- Response body: ProblemDetails with "Authentication required" detail

### 1.2 JWT Bearer Authentication - 401 Unauthorized (Invalid/Expired Token)
**File:** `screenshots/part-d-401-invalid-token.png`
**Description:** Swagger UI showing:
- Request with invalid/expired JWT
- Response: 401 Unauthorized
- Response body: ProblemDetails with "Invalid or expired token" detail

### 1.3 JWT Bearer Authentication - 403 Forbidden (Wrong Role)
**File:** `screenshots/part-d-403-wrong-role.png`
**Description:** Swagger UI showing:
- Request with Admin token trying to upload scans (Courier-only endpoint)
- Response: 403 Forbidden
- Response body: ProblemDetails with "Insufficient permissions" detail

### 1.4 JWT Bearer Authentication - 403 Forbidden (Courier ID Mismatch)
**File:** `screenshots/part-d-403-courier-mismatch.png`
**Description:** Swagger UI showing:
- Request with courierId "COURIER-001" in token but scan upload has courierId "COURIER-002"
- Response: 403 Forbidden
- Response body: ProblemDetails with "Courier ID mismatch" detail

### 1.5 JWT Bearer Authentication - 200 OK (Valid Token)
**File:** `screenshots/part-d-200-valid-token.png`
**Description:** Swagger UI showing:
- Request with valid Courier token
- Response: 200 OK with shipment data

---

## 2. Partner API Key Screenshots

### 2.1 Missing API Key - 401
**File:** `screenshots/part-d-partner-missing-key.png`
**Description:** Swagger UI showing:
- Request to `/api/v2/partner/shipments/{trackingNumber}` without X-Api-Key
- Response: 401 Unauthorized

### 2.2 Invalid API Key - 401
**File:** `screenshots/part-d-partner-invalid-key.png`
**Description:** Swagger UI showing:
- Request with invalid X-Api-Key
- Response: 401 Unauthorized

### 2.3 Valid API Key - 200 OK
**File:** `screenshots/part-d-partner-valid-key.png`
**Description:** Swagger UI showing:
- Request with valid X-Api-Key from User Secrets
- Response: 200 OK with shipment data

---

## 3. HTTPS/HSTS Screenshots

### 3.1 HTTPS Redirection
**File:** `screenshots/part-d-https-redirection.png`
**Description:** Browser/network tab showing HTTP request redirected to HTTPS with 301/302 status.

### 3.2 HSTS Header
**File:** `screenshots/part-d-hsts-header.png`
**Description:** Browser dev tools showing:
- Response header: `Strict-Transport-Security: max-age=31536000; includeSubDomains; preload`

### 3.3 Security Headers
**File:** `screenshots/part-d-security-headers.png`
**Description:** Browser dev tools showing all security headers:
- X-Content-Type-Options: nosniff
- X-Frame-Options: DENY
- X-XSS-Protection: 1; mode=block
- Referrer-Policy: strict-origin-when-cross-origin
- Content-Security-Policy: default-src 'self'

### 3.4 Swagger UI - Bearer Security Scheme
**File:** `screenshots/part-d-swagger-authorize.png`
**Description:** Swagger UI showing:
- "Authorize" button available
- Bearer security scheme configured
- JWT token input working with "Try it out"

---

## 4. Mobile App (Console) Screenshots

### 4.1 Login Screen
**File:** `screenshots/part-d-console-login.png`
**Description:** Console app showing:
- Login prompt
- Option to enter development token or use /dev/token endpoint
- Successful authentication message

### 4.2 New Scan Screen (Airplane Mode)
**File:** `screenshots/part-d-console-new-scan-offline.png`
**Description:** Console app showing:
- New Scan option selected
- Input prompts for tracking number, scan type, location, GPS
- Scan saved locally with "Pending" status
- "Pending scans" count updated

### 4.3 Pending Scans Screen
**File:** `screenshots/part-d-console-pending-scans.png`
**Description:** Console app showing:
- Pending Scans option selected
- List of pending scans with tracking numbers, scan types, timestamps
- Pending count displayed

### 4.4 Airplane Mode - Scans Saved Locally
**File:** `screenshots/part-d-console-airplane-mode.png`
**Description:** Console app showing:
- Multiple scans created while "offline" (simulated)
- All scans show syncStatus = Pending
- Pending count increases

### 4.5 Automatic Sync After Reconnect
**File:** `screenshots/part-d-console-auto-sync.png`
**Description:** Console app showing:
- "Network restored" message (simulated)
- SyncService processing pending scans in batches of 20
- Scans marked Synced/Failed based on API response
- Pending count decreases to 0

### 4.6 Sync Status Screen
**File:** `screenshots/part-d-console-sync-status.png`
**Description:** Console app showing:
- Sync Status option selected
- Last sync time
- Circuit breaker state (Closed/Open/HalfOpen)
- Failure count
- Pending scans count

---

## 5. Circuit Breaker Screenshots

### 5.1 Circuit Breaker Closed (Normal)
**File:** `screenshots/part-d-circuit-closed.png`
**Description:** Console app or logs showing:
- Circuit state: Closed
- Normal API calls succeeding
- No banner shown

### 5.2 Circuit Breaker Open (API Stopped)
**File:** `screenshots/part-d-circuit-open.png`
**Description:** Console app showing:
- Circuit state: Open (after 3 consecutive failures)
- Banner: "Server unavailable – working offline"
- New scans saved locally, not attempted to upload
- Log entries showing circuit state change

### 5.3 Circuit Breaker Half-Open (Recovering)
**File:** `screenshots/part-d-circuit-half-open.png`
**Description:** Console app showing:
- Circuit state: HalfOpen (after 30s break duration)
- Test request being sent
- On success: transitions to Closed
- On failure: transitions back to Open

### 5.4 Circuit Breaker Recovery
**File:** `screenshots/part-d-circuit-recovery.png`
**Description:** Console app showing:
- Circuit state transitions: Open → HalfOpen → Closed
- Auto-sync resumes for pending scans
- All pending scans uploaded successfully

---

## 5.5 Retry Policy Logs
**File:** `screenshots/part-d-retry-logs.png`
**Description:** Console/log output showing:
- Attempt 1 failed (network error/5xx)
- Retry 1 after ~2s (exponential backoff + jitter)
- Retry 2 after ~4s
- Retry 3 after ~8s
- Success or circuit breaker opens

---

## 6. Secure Storage Screenshots

### 6.1 Token Stored in DPAPI (SecureStorage)
**File:** `screenshots/part-d-secure-storage.png`
**Description:** Console app showing:
- Token retrieved from SecureStorage (DPAPI on Windows)
- Token not visible in plain text in files/preferences
- Token cleared on 401 response

### 6.2 Token Cleared on 401
**File:** `screenshots/part-d-token-cleared.png`
**Description:** Console app showing:
- 401 response received
- Token automatically cleared from SecureStorage
- Navigation to Login page

---

## 7. JSON Serialization Screenshots

### 7.1 Successful Response Deserialization
**File:** `screenshots/part-d-json-deserialize-success.png`
**Description:** Code/log showing:
- v2 API response with camelCase properties
- Enums as strings
- Successful deserialization to DTOs

### 7.2 Error Response Deserialization
**File:** `screenshots/part-d-json-deserialize-error.png`
**Description:** Code/log showing:
- 400/401/403/409 response deserialized to ProblemDetails/ValidationProblemDetails
- Friendly error message displayed to user
- No crash on malformed/null/unknown fields

---

## 8. End-to-End Flow Screenshots

### 8.1 Complete Offline-to-Online Flow
**File:** `screenshots/part-d-e2e-flow.png`
**Description:** Composite showing:
1. Login with development token
2. Create scans in airplane mode (saved locally, Pending)
3. Reconnect network
4. Auto-sync triggers (ConnectivityChanged)
5. Batch upload of 20 scans
6. Scans marked Synced
7. Pending count = 0
8. Circuit breaker remains Closed

---

## 9. Configuration Screenshots

### 9.1 appsettings.json (No Secrets)
**File:** `screenshots/part-d-appsettings-no-secrets.png`
**Description:** appsettings.json showing:
- No connection strings, signing keys, API keys
- Placeholder values only
- Reference to User Secrets

### 9.2 User Secrets (Development)
**File:** `screenshots/part-d-user-secrets.png`
**Description:** `dotnet user-secrets list` showing:
- Jwt:SigningKey (dev only)
- PartnerApiKeys:TestPartner (dev only)
- No production secrets

---

## 10. OpenAPI Security Scheme
**File:** `screenshots/part-d-openapi-security.png`
**Description:** Raw v2.json showing:
- components.securitySchemes with Bearer (type: http, scheme: bearer, bearerFormat: JWT)
- security requirement on all endpoints
- X-Api-Key security scheme for partner endpoint