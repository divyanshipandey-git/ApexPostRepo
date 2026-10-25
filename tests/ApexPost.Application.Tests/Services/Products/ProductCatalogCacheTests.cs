using System.Diagnostics;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Products;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;
using Xunit.Abstractions;

namespace ApexPost.Application.Tests.Services.Products;

public sealed class ProductCatalogCacheTests(ITestOutputHelper output)
{
    [Fact]
    public async Task CacheHit_AvoidsRepositoryLookup_AndMeasuresRepeatedReads()
    {
        const int iterations = 100;
        var product = new Product(new Sku("ENV001"), "Envelope", ProductCategory.Packaging,
            new Money(50m), TaxCategory.Standard);
        var repository = new CountingProductRepository(product, TimeSpan.FromMilliseconds(1));
        var cache = new ProductCatalogCache(repository, TimeProvider.System);

        Assert.Same(product, await cache.GetBySkuAsync("ENV001"));
        var callsAfterWarmup = repository.LookupCount;

        var directTimer = Stopwatch.StartNew();
        for (var index = 0; index < iterations; index++)
        {
            _ = await repository.GetBySkuAsync("ENV001");
        }
        directTimer.Stop();

        var cachedTimer = Stopwatch.StartNew();
        Product? lastCached = null;
        for (var index = 0; index < iterations; index++)
        {
            lastCached = await cache.GetBySkuAsync("ENV001");
        }
        cachedTimer.Stop();

        Assert.Same(product, lastCached);
        Assert.Equal(callsAfterWarmup + iterations, repository.LookupCount);
        output.WriteLine($"Simulated 1 ms asynchronous repository, {iterations:N0} lookups: {directTimer.Elapsed.TotalMilliseconds:F2} ms");
        output.WriteLine($"Warm ValueTask cache hits, {iterations:N0} lookups: {cachedTimer.Elapsed.TotalMilliseconds:F2} ms");
        output.WriteLine("Local demonstration only; it excludes JSON serialization, concurrent misses, expiry, and production workload variance.");
    }

    private sealed class CountingProductRepository(Product product, TimeSpan readLatency) : IProductRepository
    {
        public int LookupCount { get; private set; }

        public async Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
        {
            await Task.Delay(readLatency, cancellationToken);
            LookupCount++;
            return string.Equals(sku, product.Sku.Value, StringComparison.OrdinalIgnoreCase)
                ? product
                : null;
        }

        public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Product>>([product]);
        }
    }
}
