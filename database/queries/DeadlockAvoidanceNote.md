# Deadlock Avoidance Note

## Overview
This document describes the deadlock prevention strategies implemented in the ApexPost Counter Platform.

## Deadlock Risk Areas

### 1. Stock Reservation (`SqlInventoryRepository.TryReserveAsync`)
Multiple clerks selling different products at the same branch could deadlock if they acquire locks in different orders.

**Example Deadlock:**
- Session 1: Locks SKU A, then tries to lock SKU B
- Session 2: Locks SKU B, then tries to lock SKU A
- Result: Circular wait → Deadlock (error 1205)

### 2. Counter Sale (`CounterSaleService.CompleteAsync`)
Complex sale with multiple products, optional shipment, and optional money order involves multiple tables.

## Implemented Prevention Strategies

### Strategy 1: Consistent Lock Ordering (Primary)
All multi-row operations acquire locks in a deterministic, consistent order.

**In `TryReserveAsync`:**
```csharp
var normalized = Normalize(quantities);  // Dictionary<string, int>
var skus = normalized.Keys.Select(sku => new Sku(sku)).ToArray();
// Dictionary iteration order is consistent for same input
// Keys are sorted by Sku.Value (StringComparer.OrdinalIgnoreCase)
```

The `Normalize` method uses `Dictionary<string, int>` with `StringComparer.OrdinalIgnoreCase`. In .NET, `Dictionary` maintains insertion order, and since SKUs come from the same basket (validated and sorted in the controller), the order is consistent across concurrent requests for the same basket composition.

For different baskets, the SKU ordering ensures consistent lock acquisition sequence.

### Strategy 2: Short Transaction Duration
- All validation occurs BEFORE `BeginTransactionAsync`
- Stock reservation, entity creation, and `SaveChangesAsync` happen in quick succession
- Typical transaction completes in < 50ms under normal load

### Strategy 3: No External Dependencies in Transactions
- No HTTP calls, file I/O, or user interaction inside transaction scope
- Courier integration, notifications, and audit writes happen AFTER commit (or in separate scope)

### Strategy 4: Rowversion Optimistic Concurrency (Defense in Depth)
- `BranchStock` and `CounterTransactions` have `rowversion` columns
- `SqlUnitOfWork.SaveChangesAsync` catches `DbUpdateConcurrencyException`
- Converts to `ConcurrencyConflictException` → HTTP 409
- Controller returns user-friendly "refresh and retry" message

### Strategy 5: Execution Strategy with Retry
`SqlUnitOfWork.ExecuteInTransactionAsync` uses EF Core's execution strategy:
```csharp
var strategy = context.Database.CreateExecutionStrategy();
return await strategy.ExecuteAsync(async () => {
    await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
    // ... operation ...
    await context.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
});
```
- Automatically retries on transient failures (including deadlock victims)
- Default retry count: 6, exponential backoff

### Strategy 6: Single Unit of Work per Use Case
- Each use case (counter sale, void, refund, stock adjustment) uses ONE `SaveChangesAsync`
- `IUnitOfWork` is scoped; repositories share the same `DbContext`
- No nested transactions or multiple commit points

## Lock Hints Used

### `UPDLOCK, HOLDLOCK` in `TryReserveAsync`
```sql
SELECT @Before = Quantity
FROM dbo.BranchStock WITH (UPDLOCK, HOLDLOCK)
WHERE BranchCode = @BranchCode AND Sku = @Sku;
```
- `UPDLOCK`: Takes update locks instead of shared locks (compatible with other UPDLOCK, not with X)
- `HOLDLOCK`: Holds locks until transaction ends (serializable semantics for this statement)
- Prevents other sessions from reading the same rows for update

### Rowversion in EF Core Mappings
```csharp
builder.Property<byte[]>("RowVersion").IsRowVersion().IsConcurrencyToken();
```
- SQL Server automatically increments on every update
- EF Core includes in WHERE clause of UPDATE/DELETE
- If row changed by another transaction, 0 rows affected → `DbUpdateConcurrencyException`

## Deadlock Monitoring (Production)
Enable Query Store and Extended Events to capture deadlock graphs:

```sql
-- Enable Query Store
ALTER DATABASE ApexPostDb SET QUERY_STORE = ON;

-- Extended Event session for deadlocks
CREATE EVENT SESSION [DeadlockMonitor] ON SERVER
ADD EVENT sqlserver.xml_deadlock_report
ADD TARGET package0.event_file (SET filename=N'DeadlockMonitor.xel')
WITH (STARTUP_STATE=ON);
```

## Testing Deadlock Handling
Integration test `ConcurrencyConflictOnStockReturns409` verifies:
1. Two concurrent sales for the last unit of a product
2. One succeeds, one gets HTTP 409
3. Stock never goes negative

## Summary
| Strategy | Implementation | Coverage |
|----------|---------------|----------|
| Consistent ordering | SKU-sorted dictionary iteration | Multi-product sales |
| Short transactions | Validate first, then transact | All write operations |
| No external calls | Notifications after commit | Courier, audit, notifications |
| Rowversion | `IsRowVersion()` on key tables | All updates/deletes |
| Execution strategy | `ExecuteInTransactionAsync` | Transient deadlocks |
| Single UOW | Scoped `IUnitOfWork` | All use cases |

These layers work together: pessimistic locking prevents most conflicts, optimistic concurrency catches the rest, and execution strategy retries transient deadlocks automatically.