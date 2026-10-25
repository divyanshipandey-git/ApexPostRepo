# Product catalogue cache measurement

Run the focused micro-measurement with:

```powershell
dotnet test tests/ApexPost.Application.Tests/ApexPost.Application.Tests.csproj --no-build --filter "FullyQualifiedName~ProductCatalogCacheTests" --logger "console;verbosity=detailed"
```

The test compares 100 direct repository calls with 100 warm `ValueTask` cache hits. Its fake repository waits for 1 ms on each uncached read. In the recorded local run, direct reads took 1,522.39 ms and cache hits took 0.24 ms.

This is an illustrative latency experiment, not a production benchmark. It demonstrates avoiding repeated asynchronous repository waits; it does not measure JSON parsing, real file-system performance, cold cache population, contention, cache expiration, or workload variance. An in-memory dictionary lookup can be slower than an already-completed in-memory `Task`; the cache is intended to avoid repeated JSON reads of a catalogue that is stable during normal Web operation. The two-minute TTL limits staleness, and future catalogue write paths must call `ProductCatalogCache.Invalidate()`.
