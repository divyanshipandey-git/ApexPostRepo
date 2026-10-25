
using ApexPost.Infrastructure.Persistence;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

public sealed class JsonFileStoreDuplicateKeyTests
{
    [Fact]
    public async Task AddAsync_WhenKeyAlreadyExists_ThrowsInvalidOperationException()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var store = new JsonFileStore<JsonStoreTestEntity>(
            Path.Combine(directory.Path, "entities.json"),
            entity => entity.Id);

        await store.AddAsync(new JsonStoreTestEntity("E001", "Original"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.AddAsync(new JsonStoreTestEntity("E001", "Duplicate")));
    }

    [Fact]
    public async Task AddAsync_DuplicateKeyComparisonIsCaseInsensitive()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var store = new JsonFileStore<JsonStoreTestEntity>(
            Path.Combine(directory.Path, "entities.json"),
            entity => entity.Id);

        await store.AddAsync(new JsonStoreTestEntity("E001", "Original"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.AddAsync(new JsonStoreTestEntity("e001", "Duplicate")));
    }

    [Fact]
    public async Task AddAsync_WhenCancelledBeforeCall_ThrowsOperationCanceledException()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var store = new JsonFileStore<JsonStoreTestEntity>(
            Path.Combine(directory.Path, "entities.json"),
            entity => entity.Id);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.AddAsync(
                new JsonStoreTestEntity("E001", "Cancelled"),
                cancellation.Token));
    }
}
