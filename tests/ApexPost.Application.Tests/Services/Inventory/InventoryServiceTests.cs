using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Inventory;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Application.Tests.Services.Inventory;

public sealed class InventoryServiceTests
{
    [Fact]
    public async Task GetStockAsync_ValidValues_NormalizesInputAndReturnsStock()
    {
        var stock = new InventoryStock(
            new BranchCode("IND01"),
            new Sku("ENV001"),
            12);
        var repository = new FakeInventoryRepository(stock);
        var service = new InventoryService(repository);

        var result = await service.GetStockAsync(" ind01 ", " env001 ");

        Assert.Same(stock, result);
        Assert.Equal("IND01", repository.LastBranchCode);
        Assert.Equal("ENV001", repository.LastSku);
    }

    [Fact]
    public async Task GetStockAsync_UnknownBranchAndSku_ReturnsNull()
    {
        var repository = new FakeInventoryRepository();
        var service = new InventoryService(repository);

        var result = await service.GetStockAsync("IND99", "NOPE99");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetStockAsync_BlankBranchCode_ThrowsArgumentException(
        string branchCode)
    {
        var service = new InventoryService(new FakeInventoryRepository());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetStockAsync(branchCode, "ENV001"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetStockAsync_BlankSku_ThrowsArgumentException(
        string sku)
    {
        var service = new InventoryService(new FakeInventoryRepository());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetStockAsync("IND01", sku));
    }

    [Fact]
    public async Task GetStockAsync_InvalidInputFormat_ThrowsArgumentException()
    {
        var service = new InventoryService(new FakeInventoryRepository());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetStockAsync("IND-01", "ENV001"));
    }

    [Fact]
    public async Task GetBranchStockAsync_ValidBranch_ReturnsAllBranchStock()
    {
        var stockA = new InventoryStock(
            new BranchCode("IND01"),
            new Sku("ENV001"),
            8);
        var stockB = new InventoryStock(
            new BranchCode("IND01"),
            new Sku("BOX001"),
            3);
        var repository = new FakeInventoryRepository(stockA, stockB);
        var service = new InventoryService(repository);

        var result = await service.GetBranchStockAsync(" ind01 ");

        Assert.Equal(2, result.Count);
        Assert.Equal("IND01", repository.LastBranchCode);
    }

    [Fact]
    public async Task GetStockAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var service = new InventoryService(new FakeInventoryRepository());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetStockAsync("IND01", "ENV001", cancellationTokenSource.Token));
    }

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new InventoryService(null!));
    }

    private sealed class FakeInventoryRepository : IInventoryRepository
    {
        private readonly List<InventoryStock> _stocks;

        public string? LastBranchCode { get; private set; }
        public string? LastSku { get; private set; }

        public FakeInventoryRepository(params InventoryStock[] stocks)
        {
            _stocks = [.. stocks];
        }

        public Task<InventoryStock?> GetByBranchAndSkuAsync(
            string branchCode,
            string sku,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastBranchCode = branchCode;
            LastSku = sku;

            return Task.FromResult(
                _stocks.FirstOrDefault(
                    stock => stock.BranchCode.Value == branchCode &&
                             stock.Sku.Value == sku));
        }

        public Task<IReadOnlyList<InventoryStock>> GetByBranchAsync(
            string branchCode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastBranchCode = branchCode;

            IReadOnlyList<InventoryStock> result = _stocks
                .Where(stock => stock.BranchCode.Value == branchCode)
                .ToArray();

            return Task.FromResult(result);
        }

        public Task AddAsync(
            InventoryStock entity,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _stocks.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            InventoryStock entity,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = _stocks.FindIndex(
                stock => stock.BranchCode.Value == entity.BranchCode.Value &&
                         stock.Sku.Value == entity.Sku.Value);

            if (index >= 0)
            {
                _stocks[index] = entity;
            }
            else
            {
                _stocks.Add(entity);
            }

            return Task.CompletedTask;
        }
    }
}
