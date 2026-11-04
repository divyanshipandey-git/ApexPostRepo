using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Products;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Application.Tests.Services.Products;

public sealed class ProductPriceRevisionServiceTests
{
    [Fact]
    public async Task SetCategoryPriceAsync_UsesOneExplicitUnitOfWorkTransaction()
    {
        var repository = new SpyPriceRevisionRepository();
        var unitOfWork = new SpyUnitOfWork();
        var service = new ProductPriceRevisionService(repository, unitOfWork);

        var updatedCount = await service.SetCategoryPriceAsync("packaging", 12.34m);

        Assert.Equal(3, updatedCount);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(ProductCategory.Packaging, repository.Category);
        Assert.Equal(12.34m, repository.UnitPrice!.Amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    public async Task SetCategoryPriceAsync_RejectsInvalidCategory(string category)
    {
        var service = new ProductPriceRevisionService(
            new SpyPriceRevisionRepository(),
            new SpyUnitOfWork());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SetCategoryPriceAsync(category, 10m));
    }

    [Fact]
    public async Task SetCategoryPriceAsync_RejectsNegativePrice()
    {
        var service = new ProductPriceRevisionService(
            new SpyPriceRevisionRepository(),
            new SpyUnitOfWork());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SetCategoryPriceAsync(nameof(ProductCategory.Packaging), -1m));
    }

    private sealed class SpyPriceRevisionRepository : IProductPriceRevisionRepository
    {
        public ProductCategory? Category { get; private set; }
        public Money? UnitPrice { get; private set; }

        public Task<int> SetCategoryPriceAsync(
            ProductCategory category,
            Money unitPrice,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Category = category;
            UnitPrice = unitPrice;
            return Task.FromResult(3);
        }
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int TransactionCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            return await operation(cancellationToken);
        }
    }
}
