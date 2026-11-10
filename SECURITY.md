# Security Design Document - ApexPost Logistics API

## Overview
This document describes the authentication, authorization, and transport security implementation for the ApexPost Logistics API (Part D).

## Table of Contents
1. [JWT Bearer Authentication](#1-jwt-bearer-authentication)
2. [Role-Based Authorization](#2-role-based-authorization)
3. [Partner API Keys](#3-partner-api-keys)
4. [OAuth2 Flows](#4-oauth2-flows)
5. [Transport Layer Security](#5-transport-layer-security)
6. [Development Token Endpoint](#6-development-token-endpoint)

---

## 1. JWT Bearer Authentication

### Configuration
The API uses ASP.NET Core's built-in JWT Bearer authentication with the following validation parameters:

```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = "https://apexpost.example.com",
    ValidAudience = "apexpost-api",
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
    ClockSkew = TimeSpan.FromMinutes(2),
    RoleClaimType = ClaimTypes.Role,
    NameClaimType = ClaimTypes.NameIdentifier
};
```

### Token Claims
Tokens issued by the development endpoint contain:
- `sub` / `nameid`: Courier ID
- `role`: "Courier" or "Admin"
- `courierId`: Courier ID (custom claim for authorization)
- `jti`: Unique token identifier
- `iat`: Issued at timestamp

### Token Lifetime
- Development tokens: 8 hours
- Production tokens (via real IdP): Configurable, typically 15-60 minutes with refresh tokens

### Signing Key
- Development: Symmetric key from configuration (`Jwt:SigningKey`)
- Production: Asymmetric keys (RS256) from Identity Provider JWKS endpoint

---

## 2. Role-Based Authorization

### Policies
| Policy | Required Role | Applied To |
|--------|---------------|------------|
| `CourierOnly` | Courier | Scan upload, Status PATCH |
| `AdminOnly` | Admin | DELETE shipment |
| `AuthenticatedUser` | Any authenticated | GET shipment, GET list, POST/PUT shipment |

### Courier ID Enforcement
For scan upload endpoints, the `courierId` claim in the JWT must match the `courierId` field in each scan being uploaded. A mismatch returns **403 Forbidden**.

```csharp
// In ScanEndpoints.Map()
var courierIdClaim = httpContext.User.FindFirst("courierId")?.Value;
var invalidCourierScans = request.Scans
    .Where(s => !string.Equals(s.CourierId, courierIdClaim, StringComparison.OrdinalIgnoreCase))
    .Select(s => s.ScanId)
    .ToList();

if (invalidCourierScans.Count > 0)
{
    return Results.Problem(
        detail: $"Scans {string.Join(", ", invalidCourierScans)} have courierId that does not match your token",
        statusCode: StatusCodes.Status403Forbidden,
        title: "Forbidden");
}
```

### Error Responses
| Scenario | Status Code | Response |
|----------|-------------|----------|
| Missing token | 401 | `ProblemDetails` with title "Unauthorized" |
| Invalid/expired token | 401 | `ProblemDetails` with title "Unauthorized" |
| Wrong role | 403 | `ProblemDetails` with title "Forbidden" |
| Courier ID mismatch | 403 | `ProblemDetails` with title "Forbidden" |

---

## 3. Partner API Keys

### Endpoint
```
GET /api/v2/partner/shipments/{trackingNumber}
```

### Authentication
- Header: `X-Api-Key: <partner-api-key>`
- Validated by `ApiKeyEndpointFilter` middleware
- Keys stored in User Secrets / Configuration (never committed)

### Validation
- Constant-time comparison using `CryptographicOperations.FixedTimeEquals`
- Missing or invalid key → **401 Unauthorized**
- Unconfigured key on server → **500 Internal Server Error** (fail-closed)

### Key Management
- Generated externally (e.g., `openssl rand -hex 32`)
- Stored in Azure Key Vault / AWS Secrets Manager in production
- Rotated periodically
- Scoped to read-only partner endpoint

---

## 4. OAuth2 Flows

### 4.1 Authorization Code + PKCE (Courier Mobile App)

```
+--------+                               +---------------+
|        |---(A) Authorization Request-->|   Resource    |
|        |                               |     Owner     |
|        |<-(B) Authorization Grant------|   (Courier)   |
|        |                               |               |
|        |                               +---------------+
|        |
|        |---(C) Authorization Code----->| Authorization |
| Client |                               |     Server    |
|        |<-(D) Access Token + Refresh---|               |
|        |                               +---------------+
|        |
|        |---(E) Access Token----------->| Resource      |
|        |                               |    Server     |
|        |<-(F) Protected Resource--------|    (API)      |
+--------+                               +---------------+
```

**Steps:**
1. App opens system browser to `/authorize?response_type=code&client_id=...&redirect_uri=...&code_challenge=...&code_challenge_method=S256`
2. Courier authenticates with Identity Provider (username/password, MFA, etc.)
3. IdP redirects back to app with `code`
4. App exchanges `code` + `code_verifier` for `access_token` + `refresh_token` at `/token`
5. App uses `access_token` (JWT) as Bearer token for API calls
6. When `access_token` expires, app uses `refresh_token` to get new tokens

**Why PKCE?** Prevents authorization code interception attacks on mobile devices where custom URI schemes can be hijacked.

### 4.2 Client Credentials (Partner Systems)

```
+---------+                                  +---------------+
|         |---(A) Client Credentials-------->| Authorization |
| Partner |                                  |     Server    |
| System  |<-(B) Access Token----------------|               |
|         |                                  +---------------+
|         |
|         |---(C) Access Token-------------->| Resource      |
|         |                                  |    Server     |
|         |<-(D) Protected Resource----------|    (API)      |
+---------+                                  +---------------+
```

**Steps:**
1. Partner system sends `client_id` + `client_secret` to `/token` with `grant_type=client_credentials`
2. IdP validates credentials and returns `access_token` (JWT)
3. Partner uses `access_token` as Bearer token (or API Key for read-only endpoints)

**Use Case:** Server-to-server integration where no user interaction is involved.

### 4.3 Token Details

| Token Type | Lifetime | Storage | Use Case |
|------------|----------|---------|----------|
| Access Token (JWT) | 15-60 min | Memory (app) | API Authorization |
| Refresh Token | 7-30 days | Secure Storage (app) | Obtain new access tokens |
| API Key | 90-365 days | Server config | Partner read-only access |

### 4.4 Identity Provider Roles
- **Courier**: Mobile app users, can upload scans, update status
- **Admin**: Web portal admins, can delete shipments
- **Partner**: External systems, read-only shipment access via API Key

---

## 5. Transport Layer Security

### HTTPS Enforcement
```csharp
app.UseHttpsRedirection();  // All environments
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();  // HSTS in production
}
```

### HSTS (HTTP Strict Transport Security)
- Enabled in non-Development environments
- Max-age: 1 year (31536000 seconds)
- Includes subdomains
- Preload eligible

### Why Bearer Tokens Must Never Travel Over HTTP
1. **Token Theft**: JWTs contain claims that identify the user. Over HTTP, they're visible to anyone on the network (MITM).
2. **Replay Attacks**: Stolen tokens can be replayed to impersonate the user.
3. **No Encryption**: JWTs are base64-encoded, not encrypted. Claims are readable by anyone who intercepts them.
4. **Compliance**: PCI DSS, GDPR, and other standards require encryption in transit for authentication credentials.

### Security Headers
```csharp
context.Response.Headers["X-Content-Type-Options"] = "nosniff";
context.Response.Headers["X-Frame-Options"] = "DENY";
context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'";
```

`X-XSS-Protection` is deprecated (it can introduce vulnerabilities in old browsers and is ignored by modern ones), so it is not sent; the Content-Security-Policy is the real protection. The security-headers middleware runs before routing, CORS, rate limiting and authentication, so the headers are also present on 401, 403, 404 and 429 responses. Middleware order: exception handler → HSTS (non-Development only) → HTTPS redirection → security headers → status-code pages → routing → CORS → rate limiter → authentication → authorization → endpoints.

---

## 6. Development Token Endpoint

### Endpoint
```
POST /dev/token
Content-Type: application/json

{
  "courierId": "COURIER-001",
  "role": "Courier"
}
```

### Response
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 28800
}
```

### Security Notes
- **Only mapped in the Development environment**
- **Local requests only**: callers whose remote address is not loopback get `403`
- **No arbitrary identities**: the courier id must be listed in `DevTokens:CourierIds` (Courier role) or `DevTokens:AdminIds` (Admin role) in `appsettings.Development.json`, otherwise `403`
- Tokens are also accepted **only** in the `Authorization: Bearer` header (the former `access_token` query-string option was removed)
- Returns symmetric-key signed JWT (HS256)
- Not suitable for production
- In production, replace with real Identity Provider (Azure AD, Auth0, Keycloak, etc.)

### Migration to Production IdP
To use a real Identity Provider instead of development tokens:

1. **Register API in IdP** as a resource/API with scopes
2. **Configure JWKS endpoint** in API: `options.Authority = "https://idp.example.com/realms/apexpost"`
3. **Remove development token endpoint** (`/dev/token`)
4. **Update mobile app** to use Authorization Code + PKCE flow
5. **Configure partner systems** for Client Credentials flow
6. **Use asymmetric keys** (RS256) instead of symmetric (HS256)
6. **Enable token revocation** and introspection endpoints
7. **Set up audit logging** for token issuance and usage

---

## Summary

| Component | Implementation | Production Ready |
|-----------|----------------|------------------|
| JWT Authentication | ASP.NET Core JwtBearer | ✅ With real IdP |
| Role Authorization | Policy-based (`CourierOnly`, `AdminOnly`) | ✅ |
| Courier ID Check | Custom middleware in scan upload | ✅ |
| Partner API Keys | Endpoint filter with constant-time compare | ✅ |
| OAuth2 PKCE | Documented, not implemented (requires IdP) | ⚠️ Needs IdP |
| OAuth2 Client Credentials | Documented, not implemented (requires IdP) | ⚠️ Needs IdP |
| HTTPS/HSTS | Enforced, HSTS in prod | ✅ |
| Dev Token Endpoint | Available in Development only | ❌ Dev only |

---

*Document Version: 1.0*  
*Last Updated: 2026-11-08*

---

## 7. Secrets and configuration

No secret is committed. `appsettings.json` has no connection string, signing key or API key; the API fails fast at startup if `Jwt:SigningKey` is missing.

| Setting | How to provide it |
|---|---|
| `ConnectionStrings:ApexPost` | `dotnet user-secrets set "ConnectionStrings:ApexPost" "..." --project src/ApexPost.Api` or env var `ConnectionStrings__ApexPost` |
| `Jwt:SigningKey` (32+ chars) | `dotnet user-secrets set "Jwt:SigningKey" "..."` or env var `Jwt__SigningKey` |
| `PartnerApiKey` | `dotnet user-secrets set "PartnerApiKey" "..."` or env var `PartnerApiKey` |
| `DevTokens:CourierIds` / `AdminIds` | Development only, in `appsettings.Development.json` |

## 8. Courier app token storage

The courier app (`src/ApexPost.Courier.Console`) keeps its JWT in a file encrypted with Windows DPAPI (`DataProtectionScope.CurrentUser`), behind the `ITokenStorage` interface. This is the console-app equivalent of MAUI `SecureStorage`: the token is never stored in clear text and only the same Windows user can decrypt it. A MAUI head would register a `SecureStorage`-backed `ITokenStorage` instead (Keychain / Keystore) without any other change.
