# Screenshots - Part C: Swagger/OpenAPI Integration

## 1. Swagger UI - Version Dropdown
**File:** `screenshots/part-c-swagger-dropdown.png`
**Description:** Swagger UI showing the version dropdown with "v1 (deprecated)" and "v2" options selected.

## 2. Swagger UI - v1 (Deprecated)
**File:** `screenshots/part-c-swagger-v1.png`
**Description:** Swagger UI showing v1 endpoints with deprecated badge. Shows all v1 endpoints: GET /api/v1/shipments, POST /api/v1/shipments, GET /api/v1/shipments/{trackingNumber}, etc.

## 3. Swagger UI - v2 (Current)
**File:** `screenshots/part-c-swagger-v2.png`
**Description:** Swagger UI showing v2 endpoints. Shows v2 endpoints with recipient nested object and latestScan summary in responses.

## 4. v1 JSON Contract
**File:** `screenshots/part-c-v1-contract.png`
**Description:** Raw JSON of `/openapi/v1.json` showing v1 specification with deprecated version flag.

## 5. v2 JSON Contract
**File:** `screenshots/part-c-v2-contract.png`
**Description:** Raw JSON of `/openapi/v2.json` showing v2 specification with recipient nested object and latestScan summary.

---

## API Response Screenshots

### 1. 201 Created with Location Header
**File:** `screenshots/part-c-201-created.png`
**Description:** Swagger UI "Try it out" response showing:
- Status: 201 Created
- Location header: `/api/v2/shipments/APX-20240101`
- Response body with shipment details

### 2. 400 Bad Request - ValidationProblemDetails
**File:** `screenshots/part-c-400-validation.png`
**Description:** Swagger UI "Try it out" response showing:
- Status: 400 Bad Request
- Response body: ValidationProblemDetails with field-level errors
- Example: trackingNumber format invalid, weight out of range

### 3. 404 Not Found
**File:** `screenshots/part-c-404-notfound.png`
**Description:** Swagger UI "Try it out" response showing:
- Status: 404 Not Found
- Response body: ProblemDetails with "Shipment not found" detail

### 4. 409 Conflict
**File:** `screenshots/part-c-409-conflict.png`
**Description:** Swagger UI "Try it out" response showing:
- Status: 409 Conflict
- Response body: ProblemDetails with "Duplicate tracking number" or "Invalid status transition" detail

### 5. 415 Unsupported Media Type
**File:** `screenshots/part-c-415-media-type.png`
**Description:** Swagger UI "Try it out" response showing:
- Status: 415 Unsupported Media Type
- Response body: ProblemDetails

---

## Version-Specific Endpoints

### v1 Endpoints
**File:** `screenshots/part-c-v1-endpoints.png`
**Description:** Swagger UI showing all v1 endpoints grouped under "Shipments" and "Scans" tags with operationIds.

### v2 Endpoints
**File:** `screenshots/part-c-v2-endpoints.png`
**Description:** Swagger UI showing all v2 endpoints with:
- Recipient nested object in request/response
- latestScan summary in shipment response
- status as string enum

---

## Generated SDK Screenshots

### NSwag Configuration
**File:** `screenshots/part-c-nswag-config.png`
**Description:** nswag.json configuration file showing v2.json input and C# client output settings.

### Generated Client Project
**File:** `screenshots/part-c-generated-client.png`
**Description:** ApexPost.Client project structure showing generated client classes.

### Console Consumer
**File:** `screenshots/part-c-console-consumer.png`
**Description:** Console consumer output showing:
- Creating a shipment via generated client
- Fetching the shipment
- Uploading two scans
- Handling 404 through generated exception type

---

## XML Documentation & Annotations

### XML Comments in Controller
**File:** `screenshots/part-c-xml-comments.png`
**Description:** Code showing XML comments on actions with `<summary>`, `<param>`, `<response>`, `<returns>`.

### ProducesResponseType Attributes
**File:** `screenshots/part-c-produces-response-type.png`
**Description:** Code showing `[ProducesResponseType]` attributes for every status code.

### Tags and EndpointSummary
**File:** `screenshots/part-c-tags-endpoint-summary.png`
**Description:** Code showing `[Tags("Shipments")]`, `[Tags("Scans")]`, and `[EndpointSummary]` attributes.