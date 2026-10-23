
using ApexPost.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

public sealed class JsonInventoryRepositoryTests
{
    [Fact]
    public async Task GetByBranchAndSkuAsync_WhenMissing_ReturnsNull()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonInventoryRepository(directory.CreateOptions());

        var result = await repository.GetByBranchAndSkuAsync("BR001", "ENV001");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByBranchAsync_WhenEmpty_ReturnsEmptyCollection()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonInventoryRepository(directory.CreateOptions());

        var result = await repository.GetByBranchAsync("BR001");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByBranchAndSkuAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonInventoryRepository(directory.CreateOptions());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetByBranchAndSkuAsync(
                "BR001", "ENV001", cancellation.Token));
    }
}
