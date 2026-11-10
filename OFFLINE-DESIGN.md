# OFFLINE-DESIGN.md

## ApexPost Courier Offline - Design Note

### Why the App Saves Locally First

The courier offline application implements an **offline-first persistence strategy** where every scan is saved to the local Couchbase Lite database before any network synchronization is attempted. This design decision is driven by the core requirement that couriers operate in areas with unreliable or intermittent network connectivity.

**Key reasons for local-first persistence:**

1. **Availability during outages**: Rural courier routes frequently lose connectivity. By persisting locally first, the courier can continue scanning pickups, in-transit updates, and deliveries without interruption. The app remains fully functional regardless of network state.

2. **Data durability**: Couchbase Lite provides ACID guarantees on the local device. Once a scan is acknowledged in the UI, it's durably stored on the device's storage, eliminating the risk of data loss during unexpected app termination or device power loss.

3. **Immediate user feedback**: The UI can confirm scan creation instantly (typically < 10ms) without waiting for network round-trips, providing a responsive experience that matches user expectations for a mobile scanning workflow.

4. **Conflict-free local operations**: Since the local database is single-user (one courier, one device), there are no concurrent modification conflicts to resolve locally. Conflicts only arise during synchronization with the server.

### Conflict Resolution Strategy

The system uses a **server-decides (last-write-wins with server authority)** conflict resolution model:

- **Local writes**: Always succeed and are marked with `syncStatus = Pending`.
- **Synchronization**: The `OutboxService` reads pending scans and attempts to upload them via `IShipmentUploader`.
- **Server authority**: The server is the system of record. If a scan is modified on the server after local creation (e.g., by another courier or back-office staff), the server's version wins during sync.
- **Conflict detection**: The upload endpoint uses optimistic concurrency (e.g., ETags or version numbers). If the server rejects an upload due to a conflict, the scan is marked `SyncStatus = Failed` and the courier is notified to review.
- **Manual resolution**: Failed syncs require courier intervention—typically re-scanning or contacting dispatch—rather than automatic merge, because shipment scan data has legal/compliance implications and must be accurate.

**Why not last-write-wins (LWW) purely on timestamp?**
LWW based solely on `scannedAtUtc` timestamps is unsafe because:
- Device clocks can drift or be manually set incorrectly
- Network latency means the "last write" might not reflect the true chronological order
- Legal audit trails require server-authoritative sequencing

The server-decides approach ensures a single source of truth while preserving the offline capability.

### Why an Embedded Database Must Not Be Shared by Several Processes

Couchbase Lite is designed as an **embedded, single-process database**. Sharing the same database files across multiple processes leads to:

1. **Lock contention and corruption**: Couchbase Lite uses file-level locking (SQLite WAL mode) that assumes a single writer. Multiple processes opening the same database files will corrupt the WAL/SHM files or cause lock failures.

2. **Change notification unreliability**: The collection change listener (`AddChangeListener`) is in-process only. A second process modifying the database won't trigger listeners in the first process, leading to stale UI state.

3. **No inter-process coordination**: Couchbase Lite has no built-in IPC mechanism for coordinating transactions, schema migrations, or compaction across processes.

4. **Security isolation**: Each courier device runs one app instance. Sharing the database would violate the principle of least privilege and could expose scan data to unauthorized processes.

**Correct architecture for multi-process scenarios:**
If multiple processes need access (e.g., a background sync service + foreground UI), use **Couchbase Sync Gateway** as a central server that both processes replicate to, or implement a local IPC layer (named pipes, gRPC) with a single database owner process.

---

## Implementation Summary

| Component | Purpose |
|-----------|---------|
| `CourierDatabase` | Wrapper around Couchbase Lite `Database`, manages lifecycle, indexes, and change listeners |
| `IScanRepository` / `CouchbaseScanRepository` | CRUD + SQL++ queries with parameters for scan documents |
| `ShipmentScan` | Domain entity with `Id` format `scan::{Guid}` |
| `OutboxService` / `IShipmentUploader` | Offline-first sync pattern with pending/synced/failed states |
| `FakeShipmentUploader` | Test double simulating 90% success rate |
| Console Harness | Interactive menu: Add, Find by Tracking, List Pending, Simulate Sync, Delete |

### SQL++ Queries with Parameters
- **By tracking number**: `WHERE trackingNumber = $trackingNumber ORDER BY scannedAtUtc ASC`
- **By sync status**: `WHERE syncStatus = $syncStatus`
- **Counts by type per courier**: `SELECT scanType, COUNT(*) WHERE courierId = $courierId GROUP BY scanType`

### Value Indexes Created
- `idx_trackingNumber` on property `trackingNumber`
- `idx_syncStatus` on property `syncStatus`

### Change Listener
Registered on the `scans` collection, logs changed document IDs and updates live pending count. Properly disposed with `RemoveChangeListener` in `CourierDatabase.Dispose()`.