# Product catalogue cache measurement

Run the focused micro-measurement with:

```powershell
dotnet test tests/ApexPost.Application.Tests/ApexPost.Application.Tests.csproj --no-build --filter "FullyQualifiedName~ProductCatalogCacheTests" --logger "console;verbosity=detailed"
```

The test compares 100 direct repository calls with 100 warm `ValueTask` cache hits. Its fake repository waits for 1 ms on each uncached read. In the recorded local run, direct reads took 1,522.39 ms and cache hits took 0.24 ms.

This is an illustrative latency experiment, not a production benchmark. It demonstrates avoiding repeated asynchronous repository waits; it does not measure SQL query latency, cold cache population, contention, cache expiration, or workload variance. The test uses a fake delayed repository; in the Web runtime the cache wraps the scoped SQL catalogue repository and avoids repeat reads of a catalogue that is stable during normal operation. An in-memory dictionary lookup can be slower than an already-completed in-memory `Task`. The two-minute TTL limits staleness, and future catalogue write paths must call `ProductCatalogCache.Invalidate()`.
