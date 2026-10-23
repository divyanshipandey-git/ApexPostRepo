
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class JsonProductRepository : IProductRepository
{
    private readonly JsonFileStore<Product> _store;

    public JsonProductRepository(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        _store = new JsonFileStore<Product>(
            Path.Combine(dataDirectory, "products.json"),
            product => product.Sku.Value);
    }

    public Task<Product?> GetBySkuAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        return _store.GetByKeyAsync(sku.Trim(), cancellationToken);
    }

    public Task<IReadOnlyList<Product>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        _store.GetAllAsync(cancellationToken);

    public Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default) =>
        _store.AddAsync(product, cancellationToken);

    public Task UpdateAsync(
        Product product,
        CancellationToken cancellationToken = default) =>
        _store.UpdateAsync(product, cancellationToken);
}
