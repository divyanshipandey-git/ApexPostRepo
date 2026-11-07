# ApexPost Logistics API - Endpoint Reference

## Overview
This document provides a comprehensive reference for all API endpoints, status codes, and descriptions.

## Base URLs
- **v1 (Deprecated)**: `/api/v1`
- **v2 (Current)**: `/api/v2`

## Authentication
All endpoints require Bearer token authentication:
```
Authorization: Bearer <token>
```

## Common Headers
| Header | Description |
|--------|-------------|
| `x-api-version` | API version (1 or 2) |
| `Accept` | Must be `application/json` |
| `Content-Type` | Must be `application/json` for POST/PUT/PATCH |

## Standard Response Codes
| Code | Description |
|------|-------------|
| 200 | Successful GET/PUT/PATCH |
| 201 | Successful POST (Created) |
| 204 | Successful DELETE/PATCH |
| 400 | Validation failure |
| 401 | Unauthorized |
| 404 | Not found |
| 409 | Conflict (duplicate, invalid transition) |
| 415 | Unsupported Media Type |
| 429 | Too Many Requests |
| 500 | Internal Server Error |

---

## Shipments Endpoints

### GET /api/v{version}/shipments
**Get paged list of shipments**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| page | integer | No | 1 | Page number (1-based) |
| pageSize | integer | No | 20 | Items per page (max 50) |
| status | string | No | - | Filter by status |
| city | string | No | - | Filter by recipient city |

**Success Response (200):**
```json
{
  "items": [...],
  "page": 1,
  "pageSize": 20,
  "totalCount": 100,
  "totalPages": 5
}
```

---

### GET /api/v{version}/shipments/{trackingNumber}
**Get single shipment by tracking number**

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| trackingNumber | string | Yes | Tracking number (format: APX-YYYYMMDD) |

**Response (200):** ShipmentDetail or V2ShipmentDetail

---

### POST /api/v{version}/shipments
**Create a new shipment**

**Request Body:** CreateShipmentRequest

**Required Fields:**
- `trackingNumber` (format: `APX-YYYYMMDD`)
- `weightKg` (0.1 - 70)
- `recipientName`
- `recipientPhone` (E.164 format)
- `recipientAddress`
- `recipientCity`

**Success Response (201):**
```json
{
  "trackingNumber": "APX-20241107",
  ...
}
```
**Headers:** `Location: /api/v1/shipments/APX-20241107`

---

### PUT /api/v{version}/shipments/{trackingNumber}
**Update shipment details**

**Restrictions:** Only allowed while status is `Created`

**Request Body:** UpdateShipmentRequest (all fields optional)

---

### PATCH /api/v{version}/shipments/{trackingNumber}/status
**Update shipment status**

**Allowed Transitions:**
```
Created → PickedUp → InTransit → OutForDelivery → Delivered
Created → PickedUp → InTransit → OutForDelivery → Failed
Created → Failed
```

**Request Body:** UpdateStatusRequest
```json
{
  "status": "PickedUp",
  "reason": "Picked up from warehouse",
  "recipientSignature": "John Doe",
  "location": { "latitude": 40.7128, "longitude": -74.0060 }
}
```

**Special Rules:**
- `Delivered` requires `recipientSignature`
- `Failed` requires `reason`

---

### DELETE /api/v{version}/shipments/{trackingNumber}
**Delete a shipment**

**Restrictions:** Only allowed while status is `Created`

---

## Scans Endpoints

### POST /api/v{version}/shipments/{trackingNumber}/scans
**Bulk upload offline scans (idempotent)**

**Headers:** `Content-Type: application/json` (required)

**Request Body:** ScanUploadRequest
```json
{
  "scans": [
    {
      "scanId": "scan::uuid",
      "scanType": "Pickup",
      "courierId": "COURIER-001",
      "location": "Warehouse A",
      "scannedAtUtc": "2024-11-07T10:30:00Z",
      "notes": "Picked up from warehouse",
      "gpsCoordinates": { "latitude": 40.7128, "longitude": -74.0060 }
    }
  ]
}
```

**Idempotency:** Duplicate `scanId` values are skipped (counted in `duplicatesSkipped`)

**Response (200):**
```json
{
  "accepted": 2,
  "duplicatesSkipped": 0,
  "rejected": 0,
  "rejectionDetails": [],
  "totalProcessed": 2
}
```

**Rejection Reasons:**
- Invalid scan sequence
- Scan time in future
- Missing required fields (signature for Delivered, reason for Failed)

---

### GET /api/v{version}/shipments/{trackingNumber}/scans
**Get scan history for a shipment**

Returns all scans ordered by `scannedAtUtc` ascending.

---

## Data Types

### ShipmentDetail (v1)
```json
{
  "trackingNumber": "string",
  "status": "Created|PickedUp|InTransit|OutForDelivery|Delivered|Failed",
  "weightKg": 2.5,
  "recipientName": "string",
  "recipientPhone": "string",
  "recipientAddress": "string",
  "recipientCity": "string",
  "recipientPostalCode": "string",
  "recipientCountry": "string",
  "specialInstructions": "string",
  "declaredValue": 100.0,
  "createdAtUtc": "2024-11-07T10:30:00Z",
  "updatedAtUtc": "2024-11-07T11:00:00Z",
  "scans": [...]
}
```

### V2ShipmentDetail (v2)
```json
{
  "trackingNumber": "string",
  "status": "Created",
  "weightKg": 2.5,
  "recipient": {
    "name": "string",
    "phone": "string",
    "address": "string",
    "city": "string",
    "postalCode": "string",
    "country": "string"
  },
  "specialInstructions": "string",
  "declaredValue": 100.0,
  "createdAtUtc": "2024-11-07T10:30:00Z",
  "updatedAtUtc": "2024-11-07T11:00:00Z",
  "latestScan": {
    "scanType": "Pickup",
    "location": "string",
    "scannedAtUtc": "2024-11-07T10:30:00Z"
  },
  "scans": [...]
}
```

### ScanSummary (v1) / V2ScanSummary (v2)
```json
{
  "scanId": "scan::uuid",
  "scanType": "Pickup|InTransit|Delivered|Failed",
  "courierId": "string",
  "location": "string",
  "scannedAtUtc": "2024-11-07T10:30:00Z",
  "notes": "string",
  "gpsCoordinates": { "latitude": 40.7128, "longitude": -74.0060 },
  "recipientSignature": "string"
}
```

### ScanUploadResponse
```json
{
  "accepted": 2,
  "duplicatesSkipped": 0,
  "rejected": 0,
  "rejectionDetails": [
    { "scanId": "scan::uuid", "reason": "Invalid scan sequence" }
  ],
  "totalProcessed": 2
}
```

### PagedResponse / V2PagedResponse
```json
{
  "items": [...],
  "page": 1,
  "pageSize": 20,
  "totalCount": 100,
  "totalPages": 5
}
```

---

## Error Responses

### 400 Bad Request
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred",
  "status": 400,
  "instance": "/api/v1/shipments",
  "errors": {
    "trackingNumber": ["Tracking number must match format APX-YYYYMMDD"],
    "weightKg": ["Weight must be between 0.1 and 70 kg"]
  }
}
```

### 404 Not Found
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Resource Not Found",
  "status": 404,
  "detail": "Shipment APX-99999999 not found",
  "instance": "/api/v1/shipments/APX-99999999"
}
```

### 409 Conflict
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "Shipment with tracking number APX-20241107 already exists",
  "instance": "/api/v1/shipments"
}
```

### 415 Unsupported Media Type
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.13",
  "title": "Unsupported Media Type",
  "status": 415,
  "detail": "Content-Type must be application/json",
  "instance": "/api/v1/shipments/APX-20241107/scans"
}
```

### 429 Too Many Requests
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Try again later.",
  "instance": "/api/v1/shipments"
}
```

### 500 Internal Server Error
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred",
  "instance": "/api/v1/shipments",
  "traceId": "abc123"
}
```

---

## Rate Limiting

| Policy | Limit | Window |
|--------|-------|--------|
| Global | 100 req/min | 1 minute |
| Scan Upload (strict) | 20 req/min | 1 minute |

Response headers:
- `X-RateLimit-Limit`: Limit
- `X-RateLimit-Remaining`: Remaining
- `X-RateLimit-Reset`: Reset time (Unix timestamp)

---

## Security

- All endpoints require HTTPS
- JWT Bearer token authentication
- CORS restricted to configured origins
- Security headers: CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy
- No stack traces in production errors

---

## Versioning

| Version | Status | Base Path | Deprecated |
|---------|--------|-----------|------------|
| v1 | Deprecated | `/api/v1` | Yes |
| v2 | Active | `/api/v2` | No |

**Headers:**
- `api-supported-versions: 1, 2`
- `api-deprecated-versions: 1`
- `Deprecation: true` (v1)
- `Sunset: Sat, 01 Jan 2027 00:00:00 GMT` (v1)