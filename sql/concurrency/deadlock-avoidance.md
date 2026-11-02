# Blocking and deadlock demonstrations

Use a disposable `ApexPost_SqlIT_*` database with an existing `(BranchCode, Sku)` stock row. The scripts reject other catalog names and do not modify data.

## Reproduce blocking

1. Open two query windows connected to the same test catalog.
2. Run `blocking-demo-session-1.sql` in Session 1. It reads a stock row using `UPDLOCK, HOLDLOCK` and holds the transaction for 25 seconds.
3. While Session 1 is waiting, run `blocking-demo-session-2.sql` in Session 2. Its `READCOMMITTEDLOCK` read requests a shared lock and waits for Session 1's update lock.
4. Session 2 proceeds when Session 1 rolls back after the delay. To end early, cancel Session 1; SQL Server rolls back its read-only transaction when the connection is canceled.

Both sessions use `READ COMMITTED`. The lock hints make the conflict visible even when `READ_COMMITTED_SNAPSHOT` is enabled. Without that hint, a normal `READ COMMITTED` read may use row versions and not block when RCSI is enabled. `SNAPSHOT` reads likewise generally avoid this reader/writer wait, while writes still need locks and can conflict.

While Session 2 is waiting, inspect the request from a separate session:

```sql
SELECT session_id, blocking_session_id, wait_type, wait_time, wait_resource, status
FROM sys.dm_exec_requests
WHERE blocking_session_id <> 0;
```

Viewing other sessions may require `VIEW SERVER STATE` (or its SQL Server-version equivalent). Do not grant that privilege to the runtime application login.

## Deadlock avoidance

- Access tables and rows in a consistent order across workflows (for example, branch stock before transaction-linked movement rows).
- Keep transactions short; do not perform network calls, notifications, or user interaction while holding SQL locks.
- Maintain indexes for selective predicates and foreign-key joins so transactions lock fewer rows.
- Avoid unnecessary lock hints and large range scans; monitor lock escalation rather than disabling it globally.
- Retry deadlock victims only at a safe use-case boundary using bounded retry/backoff and an idempotent operation.
- A SQL deadlock victim receives error 1205 because SQL Server aborts a transaction to break a cycle. Optimistic concurrency instead detects stale rowversion state and should be surfaced as a conflict (HTTP 409) for the caller to refresh/reconcile; it is not an ordinary transient retry.
