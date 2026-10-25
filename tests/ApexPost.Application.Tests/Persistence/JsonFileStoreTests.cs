
using ApexPost.Infrastructure.Persistence;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

public sealed class JsonFileStoreTests
{
    [Fact]
    public async Task GetAllAsync_WhenFileDoesNotExist_ReturnsEmptyCollection()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var store = CreateStore(directory);

            var items = await store.GetAllAsync();

            Assert.Empty(items);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AddAsync_ThenGetByKeyAsync_ReturnsSavedEntity()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var store = CreateStore(directory);

            await store.AddAsync(new TestEntity("A1", "First"));

            var result = await store.GetByKeyAsync("A1");

            Assert.NotNull(result);
            Assert.Equal("First", result.Name);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AddAsync_WhenKeyAlreadyExists_Throws()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var store = CreateStore(directory);
            await store.AddAsync(new TestEntity("A1", "First"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.AddAsync(new TestEntity("A1", "Duplicate")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateAsync_ReplacesExistingEntity()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var store = CreateStore(directory);
            await store.AddAsync(new TestEntity("A1", "Before"));

            await store.UpdateAsync(new TestEntity("A1", "After"));

            var result = await store.GetByKeyAsync("A1");

            Assert.NotNull(result);
            Assert.Equal("After", result.Name);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateAsync_WhenKeyDoesNotExist_Throws()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var store = CreateStore(directory);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                store.UpdateAsync(new TestEntity("MISSING", "Unknown")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetByKeyAsync_IsCaseInsensitive()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var store = CreateStore(directory);
            await store.AddAsync(new TestEntity("A1", "First"));

            var result = await store.GetByKeyAsync("a1");

            Assert.NotNull(result);
            Assert.Equal("First", result.Name);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetAllAsync_WhenCancelled_Throws()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var store = CreateStore(directory);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                store.GetAllAsync(cancellation.Token));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static JsonFileStore<TestEntity> CreateStore(string directory) =>
        new(Path.Combine(directory, "test.json"), item => item.Id);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"ApexPostTests_{Guid.NewGuid():N}");

        Directory.CreateDirectory(path);
        return path;
    }

    public sealed record TestEntity(string Id, string Name);
}
