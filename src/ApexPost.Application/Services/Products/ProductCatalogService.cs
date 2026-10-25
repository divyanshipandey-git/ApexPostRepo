
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services.Products;

public sealed class ProductCatalogService
{
    private readonly ProductCatalogCache _cache;

    public ProductCatalogService(IProductRepository productRepository)
        : this(productRepository, new ProductCatalogCache(productRepository, TimeProvider.System))
    {
    }

    public ProductCatalogService(IProductRepository productRepository, ProductCatalogCache cache)
    {
        ArgumentNullException.ThrowIfNull(productRepository);
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    public ValueTask<Product?> GetBySkuAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var validatedSku = new Sku(sku);

        return _cache.GetBySkuAsync(validatedSku.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _cache.GetAllAsync(cancellationToken);
    }
}
