# ADR 0028: Shipment Tracking REST API

## Status
Accepted

## Context
The courier offline store (Part A) produces shipment scans that need a server to upload to. Customer portal and partner companies need a well-documented, stable REST API to track shipments. The API must:
- Reuse Week 3 SQL Server / EF Core services
- Be RESTful with proper HTTP semantics
- Support OpenAPI documentation and versioning
- Generate a typed C# client (Part C)

## Decision
We will build **ApexPost.Api** as an ASP.NET Core 10 minimal hosting Web API project.

### Project Structure
- `ApexPost.Api` - ASP.NET Core Web API (minimal hosting, no Startup.cs)
- Endpoints organized in `Endpoints/` static classes
- Services in `Services/` (thin controllers, business logic in services)
- DTOs in `DTOs/` (never bind directly to EF entities)
- Validation in `Validation/` (FluentValidation + custom rules)

### Endpoints (RESTful)

| Verb | Route | Purpose |
|------|-------|---------|
| GET | `/api/shipments?page=&pageSize=&status=&city=` | Paged, filtered list (pageSize default 20, max 50) |
| GET | `/api/shipments/{trackingNumber}` | Single shipment |
| POST | `/api/shipments` | Create shipment |
| PUT | `/api/shipments/{trackingNumber}` | Replace editable details |
| PATCH | `/api/shipments/{trackingNumber}/status` | Change status only |
| DELETE | `/api/shipments/{trackingNumber}` | Delete (only in Created status) |
| POST | `/api/shipments/{trackingNumber}/scans` | Bulk upload offline scans |
| GET | `/api/shipments/{trackingNumber}/scans` | Scan history |

### Status Transitions (Enforced)
```
Created → PickedUp → InTransit → OutForDelivery → Delivered / Failed
```
Invalid transitions return **409 Conflict** with allowed transitions.

### HTTP Status Codes (Correctly Used)
| Code | When |
|------|------|
| 200 OK | Successful read or update |
| 201 Created | Successful create; includes Location header |
| 204 No Content | Successful delete |
| 400 Bad Request | Validation failure (ValidationProblemDetails) |
| 404 Not Found | Unknown tracking number |
| 409 Conflict | Duplicate tracking#, invalid transition, delete non-Created |
| 415 Unsupported Media Type | Wrong content type (enforced on scan upload) |
| 500 Internal Server Error | Unhandled error via global handler |

### Request DTOs & Validation
**DTOs:** `CreateShipmentRequest`, `UpdateShipmentRequest`, `UpdateStatusRequest`, `ScanUploadRequest`

**Validation Rules:**
- Tracking number: `^APX-\d{8}$` (regex)
- Weight: 0.1 - 70 kg (Range)
- Recipient name, phone, address, city: Required
- Phone: E.164 format (`^\+?[1-9]\d{1,14}$`)
- Custom rules (IValidatableObject):
  - Scan time cannot be in the future (±5 min clock skew)
  - Delivered scan requires recipient signature
  - Failed scan requires reason in notes

**Validation Framework:** FluentValidation + `[ApiController]` automatic 400 with ValidationProblemDetails

### Idempotent Scan Upload
- Each scan carries Part A `scanId` (`scan::{Guid}`)
- Server checks existing scan IDs before insert
- Response: `{ accepted, duplicatesSkipped, rejected (with reasons) }`
- Idempotent: re-uploading same scan ID skips duplicate

### Error Handling
- **Global Exception Handler** (`IExceptionHandler`) → ProblemDetails (RFC 7807)
- Never exposes stack traces outside Development
- Adds `traceId` for debugging
- Maps exceptions to correct HTTP codes

### Security
- **CORS**: Restricted origins (portal, partner, localhost)
- **Rate Limiting**: 
  - Global: 100 req/min per IP
  - Strict (scan upload): 20 req/min per IP
- **Security Headers**: X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy, CSP
- **No Stack Traces in Production**: Only in Development

### API Versioning
- URL segment: `/api/v1/`
- Header: `x-api-version`
- Query: `api-version`
- Swagger: versioned docs via `Microsoft.AspNetCore.Mvc.Versioning.ApiExplorer`

### OpenAPI / Swagger
- XML documentation comments included
- JWT Bearer auth definition
- Swagger UI at `/swagger` (Development/Staging only)

### Health Checks
- `/health` endpoint
- DB context check (EF Core)
- Self check

### Domain Models
- `TrackedShipment` (APX-YYYYMMDD tracking#, weight 0.1-70kg, recipient info, status enum)
- `TrackedShipmentScan` (scan history with GPS, signature, sync status)
- Separate from Week 3 `Shipment` entity (different bounded context)

### Repositories
- `ITrackedShipmentRepository` + `SqlTrackedShipmentRepository`
- Full CRUD + queries (paged, filtered, by tracking number, scans by IDs)

### Services
- `ShipmentApiService`: CRUD + status transitions (thin, delegates to domain)
- `ScanUploadService`: Idempotent bulk upload with business rule validation

### Dependencies
- **Latest Stable Versions:**
  - Couchbase.Lite 4.1.2 (Part A)
  - ASP.NET Core 10
  - Swashbuckle.AspNetCore 6.5.0
  - FluentValidation.AspNetCore 11.3.0
  - Microsoft.AspNetCore.Mvc.Versioning 5.1.0
  - Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 9.0.0

## Consequences
### Positive
- Clean architecture: thin endpoints, services contain business logic
- Proper HTTP semantics and status codes
- Full validation with meaningful error messages
- Idempotent scan upload prevents duplicates
- Security headers, rate limiting, CORS
- Versioned API ready for future evolution
- OpenAPI docs + client generation ready (Part C)

### Negative
- New bounded context (`TrackedShipment`) separate from Week 3 `Shipment`
- Requires new migration for TrackedShipment tables
- Rate limiting may need tuning for production

## References
- Part B requirements in ApexPostWeek4.txt
- ApexPost.Api project source
- API documentation at `/swagger` (Development)