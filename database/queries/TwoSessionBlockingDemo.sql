-- Two-Session Blocking Demo with Isolation Level Explanation
-- This script demonstrates how concurrent transactions interact under different isolation levels.
-- Run this in TWO separate SSMS query windows (Session 1 and Session 2) simultaneously.

/*
================================================================================
SETUP: Run once in a separate window to create test data
================================================================================
*/
-- USE ApexPostDb;
-- GO
-- -- Ensure test branch and product exist
-- IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE BranchCode = 'DEMO')
--     INSERT INTO dbo.Branches (BranchCode, Name, Region, OpeningTime, ClosingTime, IsActive)
--     VALUES ('DEMO', 'Demo Branch', 'Test', '09:00', '17:00', 1);
--
-- IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = 'DEMO001')
--     INSERT INTO dbo.Products (Sku, Name, Category, UnitPrice, TaxCategory)
--     VALUES ('DEMO001', 'Demo Product', 'Packaging', 10.00, 'Standard');
--
-- IF NOT EXISTS (SELECT 1 FROM dbo.BranchStock WHERE BranchCode = 'DEMO' AND Sku = 'DEMO001')
--     INSERT INTO dbo.BranchStock (BranchCode, Sku, Quantity)
--     VALUES ('DEMO', 'DEMO001', 1);  -- Only 1 unit in stock!
--
-- IF NOT EXISTS (SELECT 1 FROM dbo.Counters WHERE Id = 'DEMO-CTR')
--     INSERT INTO dbo.Counters (Id, BranchCode, Name, IsActive)
--     VALUES ('DEMO-CTR', 'DEMO', 'Demo Counter', 1);
--
-- IF NOT EXISTS (SELECT 1 FROM dbo.Staff WHERE Id = 'DEMO-CLERK')
--     INSERT INTO dbo.Staff (Id, Name, Email, Role, HomeBranchCode, IsActive)
--     VALUES ('DEMO-CLERK', 'Demo Clerk', 'demo@test.com', 'CounterClerk', 'DEMO', 1);

/*
================================================================================
SESSION 1: Start a transaction, reserve the last unit, but DON'T COMMIT yet
================================================================================
-- Run this in Window 1
*/
-- SET TRANSACTION ISOLATION LEVEL READ COMMITTED;  -- Default
-- BEGIN TRANSACTION;
--
-- -- Simulate Clerk A checking stock and starting to sell the last unit
-- DECLARE @Before int;
-- SELECT @Before = Quantity
-- FROM dbo.BranchStock WITH (UPDLOCK, HOLDLOCK)  -- Takes update lock, holds until transaction ends
-- WHERE BranchCode = 'DEMO' AND Sku = 'DEMO001';
--
-- PRINT 'Session 1: Current stock = ' + CAST(@Before AS varchar(10));
-- -- Output: Session 1: Current stock = 1
--
-- -- Session 1 decrements stock (but hasn't committed)
-- UPDATE dbo.BranchStock
-- SET Quantity = 0, ModifiedAtUtc = SYSUTCDATETIME()
-- WHERE BranchCode = 'DEMO' AND Sku = 'DEMO001';
--
-- PRINT 'Session 1: Stock decremented to 0. Transaction held open...';
-- -- At this point, Session 1 holds an EXCLUSIVE lock on the BranchStock row.
-- -- The lock will be released only on COMMIT or ROLLBACK.
--
-- -- WAIT HERE - DO NOT COMMIT YET
-- -- Switch to Session 2 and run its script
-- -- Then come back and run: COMMIT TRANSACTION; or ROLLBACK TRANSACTION;

/*
================================================================================
SESSION 2: Try to sell the same product while Session 1 holds the lock
================================================================================
-- Run this in Window 2 (while Session 1 is still open)
*/
-- SET TRANSACTION ISOLATION LEVEL READ COMMITTED;  -- Default
-- BEGIN TRANSACTION;
--
-- DECLARE @Before int;
-- -- This will BLOCK because Session 1 holds an exclusive lock on the row
-- SELECT @Before = Quantity
-- FROM dbo.BranchStock WITH (UPDLOCK, HOLDLOCK)
-- WHERE BranchCode = 'DEMO' AND Sku = 'DEMO001';
--
-- -- This line won't execute until Session 1 commits/rollbacks
-- PRINT 'Session 2: Current stock = ' + CAST(@Before AS varchar(10));
--
-- IF @Before <= 0
-- BEGIN
--     PRINT 'Session 2: Insufficient stock! Rolling back.';
--     ROLLBACK TRANSACTION;
-- END
-- ELSE
-- BEGIN
--     UPDATE dbo.BranchStock SET Quantity = @Before - 1 WHERE BranchCode = 'DEMO' AND Sku = 'DEMO001';
--     PRINT 'Session 2: Sale completed.';
--     COMMIT TRANSACTION;
-- END

/*
================================================================================
EXPLANATION OF WHAT HAPPENS
================================================================================

DEFAULT ISOLATION LEVEL: READ COMMITTED

1. Session 1 starts transaction, reads stock with (UPDLOCK, HOLDLOCK)
   - Takes an UPDATE lock (compatible with shared locks, not with other update/exclusive)
   - HOLDLOCK = SERIALIZABLE semantics for this table: holds lock until transaction ends

2. Session 1 updates stock to 0
   - UPDATE lock converts to EXCLUSIVE lock
   - Row is now locked exclusively by Session 1

3. Session 2 tries to read with (UPDLOCK, HOLDLOCK)
   - Requests UPDATE lock -> BLOCKED by Session 1's EXCLUSIVE lock
   - Session 2 waits indefinitely (or until LOCK_TIMEOUT)

4. When Session 1 COMMITs:
   - Exclusive lock released
   - Session 2 acquires UPDATE lock, reads Quantity = 0
   - Session 2 sees insufficient stock, rolls back

RESULT: No oversell! The second clerk correctly sees 0 stock.

================================================================================
ALTERNATIVE: SNAPSHOT ISOLATION (if enabled at database level)
================================================================================

-- ALTER DATABASE ApexPostDb SET ALLOW_SNAPSHOT_ISOLATION ON;
-- ALTER DATABASE ApexPostDb SET READ_COMMITTED_SNAPSHOT ON;

With READ_COMMITTED_SNAPSHOT ON (statement-level snapshot):

Session 1: Same as above - takes exclusive lock on update
Session 2: SELECT without locking hints reads the COMMITTED version (1)
           But UPDATE still requires exclusive lock -> BLOCKS on Session 1's lock

With SNAPSHOT isolation (transaction-level):

SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
BEGIN TRANSACTION;

Session 1: Reads stock = 1 (from snapshot)
Session 2: Reads stock = 1 (from same snapshot - sees pre-transaction state)

Session 1: Updates stock to 0, commits
Session 2: Tries to update -> CONFLICT! SQL Server detects write-write conflict
           Throws error 3960: "Snapshot isolation transaction aborted due to update conflict"

Application must catch DbUpdateConcurrencyException and retry.

================================================================================
OUR IMPLEMENTATION USES: READ COMMITTED with (UPDLOCK, HOLDLOCK) hints
================================================================================

In SqlInventoryRepository.TryReserveAsync:
  - Uses UPDLOCK, HOLDLOCK on BranchStock SELECT
  - This takes update locks immediately, preventing other sessions from
    reading the same rows for update until the transaction completes
  - Combined with rowversion on BranchStock for optimistic concurrency
    as a second line of defense

This provides:
1. Pessimistic locking during reservation (prevents lost updates)
2. Optimistic concurrency via rowversion (catches conflicts if lock hints bypassed)
3. No overselling - stock check and decrement are atomic within the transaction

================================================================================
DEADLOCK AVOIDANCE
================================================================================

Deadlock Scenario:
  Session 1: Locks BranchStock(A), then tries to lock BranchStock(B)
  Session 2: Locks BranchStock(B), then tries to lock BranchStock(A)
  -> Circular wait -> DEADLOCK

Our Prevention Strategies:

1. CONSISTENT LOCK ORDERING
   - Always acquire locks in a deterministic order (e.g., by Sku, then BranchCode)
   - In TryReserveAsync: SKUs are normalized and processed in dictionary order
     (StringComparer.OrdinalIgnoreCase ensures consistent ordering)

2. SHORT TRANSACTIONS
   - Stock reservation, transaction creation, and SaveChanges happen quickly
   - Locks held for minimal time

3. NO USER INTERACTION IN TRANSACTIONS
   - All validation done BEFORE opening transaction
   - No external API calls inside transaction scope

4. ROWVERSION FOR OPTIMISTIC CONCURRENCY
   - If pessimistic lock is bypassed, rowversion catches the conflict
   - SqlUnitOfWork converts DbUpdateConcurrencyException to ConcurrencyConflictException
   - Controller returns HTTP 409 with user-friendly message

5. LOCK TIMEOUT (optional, configure if needed)
   -- SET LOCK_TIMEOUT 5000; -- 5 seconds, then error 1222
   -- Not currently set; relies on quick transactions and consistent ordering

6. SINGLE-UOW PATTERN
   - All writes in a use case go through one SaveChangesAsync
   - SqlUnitOfWork.ExecuteInTransactionAsync wraps in execution strategy with retry
   - Reduces window for deadlocks

================================================================================
CLEANUP
================================================================================
-- DELETE FROM dbo.CounterTransactions WHERE Id LIKE 'TXN-DEMO-%';
-- DELETE FROM dbo.TransactionLines WHERE TransactionId LIKE 'TXN-DEMO-%';
-- DELETE FROM dbo.Payments WHERE TransactionId LIKE 'TXN-DEMO-%';
-- DELETE FROM dbo.Shipments WHERE Id LIKE 'SHP-DEMO-%';
-- DELETE FROM dbo.MoneyOrders WHERE Id LIKE 'MO-DEMO-%';
-- DELETE FROM dbo.StockMovements WHERE TransactionId LIKE 'TXN-DEMO-%';
-- UPDATE dbo.BranchStock SET Quantity = 1 WHERE BranchCode = 'DEMO' AND Sku = 'DEMO001';
*/