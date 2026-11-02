
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class JsonProductRepository : IProductRepository, IProductPriceRevisionRepository
{
    private readonly JsonFileStore<Product> _store;

    public JsonProductRepository(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        _store = new JsonFileStore<Product>(
            Path.Combine(JsonPersistenceOptions.ResolveDataDirectory(dataDirectory), "products.json"),
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

    public Task<int> SetCategoryPriceAsync(
        ProductCategory category,
        Money unitPrice,
        CancellationToken cancellationToken = default) =>
        _store.MutateAllAsync(products =>
        {
            var updated = 0;
            for (var index = 0; index < products.Count; index++)
            {
                var product = products[index];
                if (product.Category != category)
                {
                    continue;
                }

                products[index] = new Product(
                    product.Sku,
                    product.Name,
                    product.Category,
                    unitPrice,
                    product.TaxCategory);
                updated++;
            }

            return (updated > 0, updated);
        }, cancellationToken);
}
