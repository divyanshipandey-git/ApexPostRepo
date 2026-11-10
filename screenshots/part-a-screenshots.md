# Screenshots - Part A: Couchbase Lite Offline Persistence

## 1. Query Results Screenshots

### Query 1: All scans for a tracking number (ordered by scannedAtUtc)
**File:** `screenshots/part-a-query-tracking-number.png`
**Description:** SQL++ query results showing all scans for tracking number "APX-20240101" ordered by scannedAtUtc ascending. Should show Pickup → InTransit → Delivered sequence.

### Query 2: All scans where syncStatus = 'Pending'
**File:** `screenshots/part-a-query-pending.png`
**Description:** SQL++ query results showing only scans with syncStatus = 'Pending'. Demonstrates parameterized query with syncStatus filter.

### Query 3: Number of scans per scanType for one courier
**File:** `screenshots/part-a-query-scan-counts.png`
**Description:** SQL++ aggregation query showing count of scans grouped by scanType for courierId "COURIER-001". Example output: Pickup: 5, InTransit: 3, Delivered: 2, Failed: 1.

## 2. Change Listener Log Screenshot
**File:** `screenshots/part-a-change-listener.png`
**Description:** Console output showing the change listener logging document IDs when scans are added/updated. Should show live "pending scans" count updating in real-time.

---

## Console Harness Menu Screenshots

### Main Menu
**File:** `screenshots/part-a-console-menu.png`
**Description:** Console application main menu showing options:
1. Add Scan
2. Find by Tracking Number
3. List Pending
4. Simulate Sync
5. Delete Scan
6. Exit

### Add Scan Operation
**File:** `screenshots/part-a-add-scan.png`
**Description:** Console input prompts for tracking number, scan type, courier ID, location, GPS coordinates, notes. Shows successful creation with scan ID.

### Find by Tracking Number
**File:** `screenshots/part-a-find-by-tracking.png`
**Description:** Console output showing all scans for a tracking number, ordered by scannedAtUtc.

### List Pending
**File:** `screenshots/part-a-list-pending.png`
**Description:** Console output showing all scans with syncStatus = Pending.

### Simulate Sync
**File:** `screenshots/part-a-simulate-sync.png`
**Description:** Console output showing OutboxService processing pending scans, marking them Synced/Failed.

### Delete Scan
**File:** `screenshots/part-a-delete-scan.png`
**Description:** Console operation deleting a scan by ID, with confirmation.

---

## Database Screenshots

### Couchbase Lite Database Structure
**File:** `screenshots/part-a-database-structure.png`
**Description:** Showing the `apexpost_courier` database with default collection containing ShipmentScan documents.

### Document Example
**File:** `screenshots/part-a-document-example.png`
**Description:** JSON document showing ShipmentScan with all required fields: id (scan::{Guid}), trackingNumber, scanType, courierId, location, scannedAtUtc, notes, syncStatus.

---

## Index Screenshots

### Value Indexes
**File:** `screenshots/part-a-value-indexes.png`
**Description:** Showing value indexes created on `trackingNumber` and `syncStatus` fields for query optimization.