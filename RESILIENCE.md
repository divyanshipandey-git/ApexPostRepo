# Resilience Design Document - ApexPost Courier App

## Overview
This document describes the resilience patterns implemented in the ApexPost Courier app (`src/ApexPost.Courier.Console`, a .NET console app) for reliable API communication over unreliable networks.

> **Console app, not MAUI.** The courier client in this repository is a console application, not a .NET MAUI project (a MAUI app cannot be built in the Linux/CI environment this work was prepared in). All resilience code (Polly pipeline, sync service, JSON context, token storage seam) is UI-agnostic and moves to a MAUI head unchanged; the MAUI-specific parts are called out below (`Connectivity`, `SecureStorage`, emulator URL). **Please confirm with your instructor that a console client is acceptable for the "mobile" part; if a MAUI app is required, it must be added on top of these services.**

## Table of Contents
1. [Retry Policy](#1-retry-policy)
2. [Circuit Breaker](#2-circuit-breaker)
3. [Timeouts](#3-timeouts)
4. [Idempotency Justification](#4-idempotency-justification)
5. [Logging](#5-logging)
6. [Configuration Summary](#6-configuration-summary)

---

## 1. Retry Policy

### Configuration
```csharp
options.Retry.MaxRetryAttempts = 3;
options.Retry.Delay = TimeSpan.FromSeconds(1);
options.Retry.BackoffType = DelayBackoffType.Exponential;
options.Retry.UseJitter = true;
```

### Retry Behavior

| Attempt | Base Delay | With Jitter (±25%) | Cumulative |
|---------|------------|---------------------|------------|
| 1 (initial) | 0s | 0s | 0s |
| 2 | 1s | 0.75s - 1.25s | ~1s |
| 3 | 2s | 1.5s - 2.5s | ~3s |
| 4 | 4s | 3s - 5s | ~7s |

### Retry Conditions
The retry policy only triggers for **transient failures**:

```csharp
options.Retry.ShouldHandle = args =>
{
    // Network-level errors (DNS, connection refused, timeout)
    if (args.Outcome.Exception is HttpRequestException)
        return ValueTask.FromResult(true);
    
    // HTTP 408 Request Timeout
    if (args.Outcome.Result?.StatusCode == HttpStatusCode.RequestTimeout)
        return ValueTask.FromResult(true);
    
    // HTTP 5xx Server Errors
    if ((int)(args.Outcome.Result?.StatusCode ?? 0) >= 500)
        return ValueTask.FromResult(true);
    
    // Never retry 4xx client errors (400, 401, 403, 404, 409, etc.)
    return ValueTask.FromResult(false);
};
```

### Why These Conditions?
- **HttpRequestException**: Network failures (no connectivity, DNS issues, connection dropped)
- **408 Request Timeout**: Server didn't respond in time, safe to retry
- **5xx Server Errors**: Temporary server issues (overload, maintenance, transient bugs)
- **Never 4xx**: Client errors indicate a bug in the request (bad auth, validation error, not found) - retrying won't help

---

## 2. Circuit Breaker

### Configuration
```csharp
options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
options.CircuitBreaker.MinimumThroughput = 5;
options.CircuitBreaker.FailureRatio = 0.5;
options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
```

### Behavior

| State | Description | Transitions |
|-------|-------------|-------------|
| **Closed** | Normal operation, requests pass through | Opens when failure ratio ≥ 50% over 30s with ≥5 requests |
| **Open** | Requests fail fast without calling server | Closes after 30s break duration |
| **Half-Open** | Test requests allowed | Closes on success, re-opens on failure |

### App Integration
The Polly circuit breaker reports its transitions to `ResilienceStateTracker` (see section 5), which `SyncService` listens to. When the circuit breaker opens:
1. The console UI shows a banner on the main menu and on the Sync Status screen: **"Server unavailable - working offline"**
2. New scans keep being saved locally with `Pending` status
3. Background sync pauses (`SyncState.CircuitOpen`)
4. After the break duration the next request is a half-open probe; if it succeeds the circuit closes, the banner disappears and sync resumes. `SyncService` also resets its own circuit flag after `Sync:CircuitBreakerResetTimeout` (30s) so a probe is always attempted.

### Why These Values?
- **30s sampling**: Long enough to detect sustained issues, short enough to react quickly
- **5 minimum throughput**: Avoids opening on sporadic single failures
- **50% failure ratio**: Balances sensitivity (too low = false positives) vs resilience (too high = delayed reaction)
- **30s break**: Gives server time to recover without hammering it

---

## 3. Timeouts

### Configuration
```csharp
options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
```

### Timeout Types

| Timeout | Value | Purpose |
|---------|-------|---------|
| **Attempt Timeout** | 10s | Max time for a single HTTP attempt (including retries) |
| **Total Request Timeout** | 30s | Max cumulative time across all retry attempts |

### Interaction with Retry
```
Attempt 1: 10s timeout → fails (network error)
    ↓ retry after 1s (with jitter)
Attempt 2: 10s timeout → fails (500 error)
    ↓ retry after 2s (with jitter)
Attempt 3: 10s timeout → fails (500 error)
    ↓ Total time: ~33s (exceeds 30s total timeout)
Request fails with TimeoutException
```

### Why These Values?
- **10s attempt**: Mobile networks can be slow; 10s accommodates high latency
- **30s total**: Prevents indefinite hanging; user sees error within reasonable time
- **Sum > Total**: Allows 3 attempts with backoff within total budget

---

## 4. Idempotency Justification

### Why Scan Upload Is Safe to Retry

The scan upload endpoint (`POST /api/v2/shipments/{trackingNumber}/scans`) is **idempotent by design** because:

#### 1. Client-Generated Scan IDs
Each scan carries a unique `scanId` in format `scan::{Guid}` generated by the courier app (Part A):
```csharp
var scan = new ScanDocumentRequest
{
    ScanId = $"scan::{Guid.NewGuid()}",  // Unique per scan
    TrackingNumber = "APX-20241107",
    ScanType = "Pickup",
    CourierId = "COURIER-001",
    // ...
};
```

#### 2. Server-Side Deduplication (Part B)
`ScanUploadService` looks up the scan ids already stored and skips them. Duplicates are reported separately and are never mixed with real rejections:
```csharp
if (existingScanIds.Contains(scanRequest.ScanId))
{
    response.DuplicatesSkipped++;
    response.DuplicateScanIds.Add(scanRequest.ScanId);   // not added to RejectionDetails
    continue;
}
```
The scan id is also the **primary key** of the scans table, so two concurrent uploads of the same scan cannot both insert it. The loser's `UniqueKeyConflictException` is handled by re-checking and counting the scan as a duplicate; if a whole request raced another one the API answers `409 Conflict` and the client simply retries (the courier app treats 409 as "try again later" and leaves the scans pending).

#### 3. Retry Safety Proof
| Scenario | First Attempt | Retry Attempt | Result |
|----------|---------------|---------------|--------|
| Network timeout after server processed | ✅ Accepted | 🔄 Duplicate skipped | **Exactly once** |
| 500 error, server didn't process | ❌ Failed | ✅ Accepted | **Exactly once** |
| Success, client didn't receive response | ✅ Accepted | 🔄 Duplicate skipped | **Exactly once** |

#### 4. Response Includes Counts
```json
{
  "accepted": 1,
  "duplicatesSkipped": 1,
  "duplicateScanIds": ["scan::..."],
  "rejected": 0,
  "rejectionDetails": [],
  "totalProcessed": 2
}
```
The client can verify exactly what happened.

---

## 5. Logging

Every retry and every circuit breaker state change is logged by callbacks registered on the Polly pipeline in `Program.cs`:

```csharp
.AddStandardResilienceHandler()
.Configure((options, sp) =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ApexPost.Resilience");
    var tracker = sp.GetRequiredService<IResilienceStateTracker>();

    options.Retry.OnRetry = args =>
    {
        logger.LogWarning("Retry {Attempt}/{Max} in {Delay:0.0}s after {Reason}",
            args.AttemptNumber + 1, options.Retry.MaxRetryAttempts, args.RetryDelay.TotalSeconds,
            args.Outcome.Exception?.GetType().Name ?? $"HTTP {(int?)args.Outcome.Result?.StatusCode}");
        return default;
    };
    options.CircuitBreaker.OnOpened     = args => { tracker.CircuitOpened(args.BreakDuration, ...); return default; };
    options.CircuitBreaker.OnHalfOpened = _    => { tracker.CircuitHalfOpened(); return default; };
    options.CircuitBreaker.OnClosed     = _    => { tracker.CircuitClosed(); return default; };
});
```

`ResilienceStateTracker` writes the log line for each transition ("Circuit breaker OPENED for 30s: ...", "HALF-OPEN: the next request is a probe", "CLOSED: the server is reachable again") and raises `CircuitStateChanged`, which `SyncService` uses to switch to `SyncState.CircuitOpen` / back to `Idle`.

### Log Levels
| Event | Level | Includes |
|-------|-------|----------|
| Retry attempt | Warning | Attempt n/max, delay, exception type or HTTP status |
| Circuit opened | Warning | Break duration, triggering exception/status |
| Circuit half-open | Information | Probe notice |
| Circuit closed | Information | Recovery |
| Network available/unavailable | Information | From `NetworkConnectivityMonitor` |
| Sync start/end | Information | Scan counts |
| Sync error | Warning/Error | Status code, tracking number |

---

## 6. Configuration Summary

### Polly Resilience Pipeline (Program.cs)
```csharp
builder.Services.AddHttpClient<IApexApiClient, ApexApiClient>((sp, client) =>
    {
        client.BaseAddress = sp.GetRequiredService<IApiEndpointResolver>().BaseAddress;   // platform specific
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<AuthHeaderHandler>()
    .AddStandardResilienceHandler()
    .Configure((options, sp) =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.ShouldHandle = args => ...;            // only transient failures
        options.Retry.OnRetry = ...;                         // logs every retry
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.OnOpened / OnHalfOpened / OnClosed = ...;   // log + update tracker
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
    });
```

### Platform specific API address
`IApiEndpointResolver` picks the base address from the `Api` configuration section:

| `Api:Platform` | Address used | Default |
|---|---|---|
| `Windows` | `Api:WindowsBaseUrl` | `https://localhost:54402` |
| `AndroidEmulator` | `Api:AndroidEmulatorBaseUrl` | `https://10.0.2.2:54402` (the emulator's route to the host machine) |
| `Auto` (default) | Android emulator on Android, Windows address otherwise | |

### Sync Service Behavior (SyncService.cs)
```csharp
public async Task<SyncResult> SyncPendingScansAsync(CancellationToken cancellationToken = default)
{
    // 1. Check connectivity & circuit breaker (reset the circuit after CircuitBreakerResetTimeout)
    // 2. Group pending scans by tracking number, then chunk each group (Sync:BatchSize, default 20)
    //    -> a request never mixes scans of different shipments
    // 3. Upload each batch with the idempotent API call
    // 4. Mark scans Synced/Failed based on the response (duplicates count as synced)
    // 5. 401 -> stop and ask for a new login; 400/403/404/422 -> mark the batch Failed with the reason
    // 6. 409 -> leave pending, retry on the next cycle; 5xx/timeout/BrokenCircuitException -> keep pending, count towards the circuit
}
```

### Connectivity Integration
`NetworkConnectivityMonitor` (an `IHostedService`) subscribes to the operating system's `NetworkChange.NetworkAvailabilityChanged` and `NetworkAddressChanged` events and calls `ISyncService.OnConnectivityChangedAsync(isConnected)` on every real transition (and once at start-up for the initial state). Coming back online triggers an immediate sync; going offline switches the UI to the offline banner. In a MAUI head the same call is made from `Connectivity.ConnectivityChanged`.

### JSON
All API traffic and the token file use a source-generated `JsonSerializerContext` (`ApexJsonContext`) over DTOs owned by the courier project (`Models/ApiContracts.cs`). The courier app has no reference to the API project or to ASP.NET Core types.

### Token storage
Tokens are stored with Windows DPAPI (`ProtectedData`, current-user scope) behind `ITokenStorage`: the console equivalent of MAUI `SecureStorage`. A MAUI head registers a `SecureStorage`-backed implementation of the same interface.

---

## Decision Rationale

| Decision | Rationale |
|----------|-----------|
| **3 retry attempts** | Balance between reliability and user wait time |
| **Exponential backoff + jitter** | Prevents thundering herd; handles temporary congestion |
| **Only retry transient errors** | 4xx errors indicate bugs - fix the code, don't retry |
| **Circuit breaker** | Prevents cascading failures; gives server breathing room |
| **30s break duration** | Long enough for server recovery, short enough for user |
| **Idempotent scan upload** | Client-generated UUIDs + server deduplication = exactly-once |
| **Encrypted token storage (DPAPI / SecureStorage on MAUI)** | Platform-backed encryption; never in logs or plain files |
| **NetworkChange trigger** | Immediate sync when connection restored, plus a periodic auto-sync as a safety net |

---

## Testing Resilience

### Manual Test Scenarios
1. **Network off**: disable the network adapter, create scans, re-enable → verify the offline banner and auto-sync
2. **Stop API**: Create scans → start API → verify sync & circuit recovery
3. **Network latency**: Use `tc`/`clumsy` to add 5s latency → verify timeout/retry
4. **500 errors**: Configure API to return 500 → verify circuit opens
5. **401 expired token**: Wait for token expiry → verify redirect to login

### Automated Tests (Future)
- Unit tests for `SyncService` with mocked `HttpMessageHandler`
- Integration tests with TestServer simulating various failure modes
- Chaos engineering: random latency/errors in staging

---

*Document Version: 1.0*  
*Last Updated: 2026-11-09*