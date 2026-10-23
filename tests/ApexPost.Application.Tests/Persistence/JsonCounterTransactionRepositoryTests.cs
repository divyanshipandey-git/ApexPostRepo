
using ApexPost.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

public sealed class JsonCounterTransactionRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_WhenMissing_ReturnsNull()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonCounterTransactionRepository(directory.CreateOptions());

        var result = await repository.GetByIdAsync("TXN001");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByBranchAsync_WhenEmpty_ReturnsEmptyCollection()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonCounterTransactionRepository(directory.CreateOptions());

        var result = await repository.GetByBranchAsync("BR001");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonCounterTransactionRepository(directory.CreateOptions());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetByIdAsync("TXN001", cancellation.Token));
    }
}
