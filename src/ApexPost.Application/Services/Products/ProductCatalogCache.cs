using System.Collections.Concurrent;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services.Products;

/// <summary>
/// Short-lived immutable catalogue cache; warm SKU hits complete synchronously via ValueTask.
/// Misses still read the repository, are not coalesced, and absent products are not cached.
/// </summary>
public sealed class ProductCatalogCache
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(2);
    private readonly IProductRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _lifetime;
    private readonly ConcurrentDictionary<string, CacheEntry> _products = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<Product>? _allProducts;
    private DateTimeOffset _allProductsExpiresAt;

    public ProductCatalogCache(
        IProductRepository repository,
        TimeProvider timeProvider,
        TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _timeProvider = timeProvider;
        _lifetime = lifetime ?? DefaultLifetime;
        if (_lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }
    }

    public ValueTask<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        cancellationToken.ThrowIfCancellationRequested();
        var key = sku.Trim();
        var now = _timeProvider.GetUtcNow();
        if (_products.TryGetValue(key, out var entry) && entry.ExpiresAt > now)
        {
            return ValueTask.FromResult<Product?>(entry.Product);
        }

        return new ValueTask<Product?>(LoadProductAsync(key, now, cancellationToken));
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var cached = Volatile.Read(ref _allProducts);
        if (cached is not null && _allProductsExpiresAt > now)
        {
            return cached;
        }

        var products = await _repository.GetAllAsync(cancellationToken);
        var snapshot = products.ToArray();
        var expiration = now + _lifetime;
        foreach (var product in snapshot)
        {
            _products[product.Sku.Value] = new CacheEntry(product, expiration);
        }

        _allProductsExpiresAt = expiration;
        Volatile.Write(ref _allProducts, snapshot);
        return snapshot;
    }

    /// <summary>Clear snapshots after a future catalogue write path changes a product.</summary>
    public void Invalidate()
    {
        _products.Clear();
        Volatile.Write(ref _allProducts, null);
        _allProductsExpiresAt = DateTimeOffset.MinValue;
    }

    private async Task<Product?> LoadProductAsync(
        string sku,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        var product = await _repository.GetBySkuAsync(sku, cancellationToken);
        if (product is not null)
        {
            _products[sku] = new CacheEntry(product, requestedAt + _lifetime);
        }

        return product;
    }

    private sealed record CacheEntry(Product? Product, DateTimeOffset ExpiresAt);
}
