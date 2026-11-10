# API Versioning Strategy

## Overview
This document describes the versioning strategy for the ApexPost Logistics API.

## Versioning Scheme
- **URL Segment Versioning**: `/api/v{version}/` (e.g., `/api/v1/`, `/api/v2/`)
- Only the URL segment is read (`UrlSegmentApiVersionReader`). Header and query-string versioning are **not** enabled.

## Version Lifecycle

### v1 (Deprecated)
- **Status**: Deprecated
- **Release Date**: Week 4 Part B
- **Deprecation Date**: Week 4 Part C
- **Sunset Date**: 2027-07-01 (configured with `options.Policies.Sunset(1.0)`; sent in the `Sunset` response header)
- **Breaking Changes in v2**: 
  1. Recipient fields grouped into nested `recipient` object
  2. Added `latestScan` summary field
  3. Status returned as string enum (not integer)
  4. Scan type returned as string enum

### v2 (Current)
- **Status**: Active
- **Release Date**: Week 4 Part C
- **Features**:
  - Nested recipient object
  - Latest scan summary
  - String enums for status and scan types
  - Enhanced scan summary with recipient signature

## Breaking vs Non-Breaking Changes

### Breaking Changes (Require New Version)
| Change | Type | Example |
|--------|------|---------|
| Remove field | Breaking | Removing `recipientName` from root |
| Change field type | Breaking | Changing `status` from int to string |
| Remove enum value | Breaking | Removing `Failed` status |
| Change required fields | Breaking | Making `recipientSignature` required |
| Change response structure | Breaking | Nesting recipient fields |

### Non-Breaking Changes (Same Version)
| Change | Type | Example |
|--------|------|---------|
| Add optional field | Non-breaking | Adding `declaredValue` |
| Add optional enum value | Non-breaking | Adding `OnHold` status |
| Add new endpoint | Non-breaking | Adding `/api/v2/reports` |
| Add optional parameter | Non-breaking | Adding `sortBy` query param |
| Add new response field | Non-breaking | Adding `latestScan` summary |

## Deprecation Process

1. **Announce Deprecation**: Document in VERSIONING.md and release notes
2. **Set Deprecation Header**: Add `Deprecation: true` and `Sunset` headers to v1 responses
3. **Communication**: Notify API consumers via email and developer portal
4. **Minimum Support Period**: 6 months minimum
5. **Monitor Usage**: Track v1 usage via analytics
6. **Sunset**: Remove v1 after sunset date

## Sunset Plan for v1
1. **Deprecation Announced**: Week 4 Part C
2. **Deprecation Header Added**: Week 4 Part C (immediate)
3. **6-Month Minimum**: Until at least Week 10 Part C
4. **Usage Monitoring**: Monthly analytics review
4. **Final Notice**: 30 days before sunset
5. **Sunset**: Remove v1 routes and documentation

## Client Migration Guide

### From v1 to v2
1. **Update Base URL**: `/api/v1/` → `/api/v2/`
2. **Request Changes**: 
   - Request bodies remain compatible
3. **Response Changes**:
   - `recipientName`, `recipientPhone`, etc. → nested under `recipient` object
   - `status` returns string instead of integer
   - New `latestScan` field in responses
   - Scan `scanType` returned as string enum
4. **Client SDK**: Regenerate from v2 OpenAPI spec

### Code Example Migration

**v1 Response:**
```json
{
  "trackingNumber": "APX-20241107",
  "status": 0,
  "recipientName": "John Doe",
  "recipientPhone": "+15551234567",
  "recipientAddress": "123 Main St",
  "recipientCity": "Springfield",
  "scans": [...]
}
```

**v2 Response:**
```json
{
  "trackingNumber": "APX-20241107",
  "status": "Created",
  "recipient": {
    "name": "John Doe",
    "phone": "+15551234567",
    "address": "123 Main St",
    "city": "Springfield",
    "country": "US"
  },
  "latestScan": {
    "scanType": "Pickup",
    "location": "Warehouse A",
    "scannedAtUtc": "2024-11-07T10:30:00Z"
  },
  "scans": [...]
}
```

## API Headers for Versioning

The version is chosen only by the URL (`/api/v1/...`, `/api/v2/...`); no request header selects it.

### Request Headers
- `Accept: application/json`

### Response Headers
- `api-supported-versions: 1.0, 2.0` (every response)
- `api-deprecated-versions: 1.0` (every response)
- `Deprecation: true` (v1 responses)
- `Sunset: Thu, 01 Jul 2027 00:00:00 GMT` (v1 responses, from the sunset policy)

## How deprecation is implemented
- `[ApiVersion("1.0", Deprecated = true)]` on the controllers marks v1 deprecated in Asp.Versioning and in the generated `/openapi/v1.json` (`deprecated: true` on each operation).
- v2 actions use `[MapToApiVersion("2.0")]` and call the v2 service methods (`GetShipmentV2Async` etc.), so v2 genuinely returns the nested `recipient` and `latestScan`.

## Client SDK Versioning

The generated C# client SDK follows semantic versioning:
- `1.x.x` - Compatible with API v1
- `2.x.x` - Compatible with API v2

## Monitoring and Metrics

Track per-version:
- Request volume
- Error rates
- Latency percentiles
- Active clients
- Deprecated version usage

## References
- [OpenAPI Specification v3.0](https://spec.openapis.org/oas/v3.0.1)
- [API Versioning Best Practices](https://docs.microsoft.com/en-us/aspnet/core/web-api/versioning)
- [Semantic Versioning 2.0.0](https://semver.org/)