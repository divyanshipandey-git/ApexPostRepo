
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Products;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Application.Tests.Services.Products;

public sealed class ProductCatalogServiceTests
{
    [Fact]
    public async Task GetBySkuAsync_ValidSku_ReturnsProduct()
    {
        var product = CreateProduct();
        var repository = new FakeProductRepository(product);
        var service = new ProductCatalogService(repository);

        var result = await service.GetBySkuAsync("env001");

        Assert.Same(product, result);
        Assert.Equal("ENV001", repository.LastRequestedSku);
    }

    [Fact]
    public async Task GetBySkuAsync_UnknownSku_ReturnsNull()
    {
        var repository = new FakeProductRepository();
        var service = new ProductCatalogService(repository);

        var result = await service.GetBySkuAsync("UNKNOWN");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetBySkuAsync_BlankSku_ThrowsArgumentException(
        string sku)
    {
        var service = new ProductCatalogService(
            new FakeProductRepository());

        Assert.Throws<ArgumentException>(
            () => service.GetBySkuAsync(sku));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsProductsFromRepository()
    {
        var product = CreateProduct();
        var repository = new FakeProductRepository(product);
        var service = new ProductCatalogService(repository);

        var result = await service.GetAllAsync();

        Assert.Single(result);
        Assert.Same(product, result[0]);
    }

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ProductCatalogService(null!));
    }

    private static Product CreateProduct() =>
        new(
            new Sku("ENV001"),
            "Envelope",
            ProductCategory.Packaging,
            new Money(50m),
            TaxCategory.Standard);

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly List<Product> _products;

        public string? LastRequestedSku { get; private set; }

        public FakeProductRepository(params Product[] products)
        {
            _products = [.. products];
        }

        public Task<Product?> GetBySkuAsync(
            string sku,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestedSku = sku;

            Product? product = _products.FirstOrDefault(
                item => item.Sku.Value == sku);

            return Task.FromResult(product);
        }

        public Task<IReadOnlyList<Product>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<Product> products = _products.ToArray();
            return Task.FromResult(products);
        }
    }
}
