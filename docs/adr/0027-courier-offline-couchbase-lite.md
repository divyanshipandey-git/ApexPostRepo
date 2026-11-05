# ADR 0027: Courier Offline Storage with Couchbase Lite

## Status
Accepted

## Context
Couriers in the field often lose network coverage but must still record pickups and deliveries. The system needs an offline-first solution that:
- Works without network connectivity
- Stores shipment scans locally as JSON documents
- Supports queries and change notifications offline
- Syncs with the central API when connectivity is restored

## Decision
We will use **Couchbase Lite 4.1.2** as the embedded NoSQL database for the courier offline store.

### Database Design
- Database name: `apexpost_courier`
- Collection: `scans` (default collection)
- Document ID format: `scan::{Guid}`
- Document fields: trackingNumber, scanType, courierId, location, scannedAtUtc, notes, syncStatus

### Sync Status Values
- `Pending` - New scan saved locally, not yet uploaded
- `Synced` - Successfully uploaded to server
- `Failed` - Upload failed, requires manual retry

### Indexes
- Value index on `trackingNumber` for query-by-tracking-number
- Value index on `syncStatus` for pending scans query

### Queries (SQL++)
All queries use **parameterized SQL++** to prevent injection:
1. All scans for a tracking number, ordered by scannedAtUtc
2. All scans where syncStatus = 'Pending'
3. Number of scans per scanType for one courier (GROUP BY)

### Offline Persistence Strategy
- **Local-first**: Every scan saved locally first with `syncStatus = Pending`
- **Outbox Pattern**: `OutboxService` reads pending scans, uploads via `IShipmentUploader`
- **Idempotent Upload**: Server uses scan ID for deduplication
- **FakeUploader**: Part A uses `FakeShipmentUploader` (90% success); Part D replaces with real API call

### Change Listener
- Registered on `scans` collection
- Logs changed document IDs
- Updates live "pending scans" count
- Properly disposed in `CourierDatabase.Dispose()`

### Conflict Resolution
**Server-decides (not LWW):**
- Server is system of record
- Optimistic concurrency via ETags/version numbers
- Conflicts → scan marked `Failed` → courier notified for manual resolution
- LWW on timestamps rejected due to clock drift and audit requirements

### Embedded Database Isolation
Couchbase Lite is **single-process only**:
- File-level locking (SQLite WAL) assumes single writer
- Change listeners are in-process only
- No IPC for transactions/schema migrations
- Multi-process requires Sync Gateway or IPC layer

## Consequences
### Positive
- True offline capability for rural couriers
- ACID guarantees on local device
- Parameterized queries prevent injection
- Proper resource cleanup via IDisposable
- Server-authoritative conflict resolution

### Negative
- Cannot share database across processes
- Requires Couchbase Lite native dependencies
- Sync Gateway not used (Part D requirement)

## References
- [Couchbase Lite Documentation](https://docs.couchbase.com/couchbase-lite/current/)
- OFFLINE-DESIGN.md
- Part A requirements in ApexPostWeek4.txt