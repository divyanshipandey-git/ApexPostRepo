
using ApexPost.Infrastructure.Persistence;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

public sealed class JsonFileStoreCrudTests
{
    [Fact]
    public async Task AddAsync_PersistsEntityThatCanBeRetrieved()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var path = Path.Combine(directory.Path, "entities.json");
        var store = new JsonFileStore<JsonStoreTestEntity>(
            path, entity => entity.Id);

        await store.AddAsync(new JsonStoreTestEntity("E001", "First"));

        var result = await store.GetByKeyAsync("E001");

        Assert.NotNull(result);
        Assert.Equal("E001", result.Id);
        Assert.Equal("First", result.Name);
    }

    [Fact]
    public async Task AddAsync_EntityCanBeReadByANewStoreInstance()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var path = Path.Combine(directory.Path, "entities.json");

        var firstStore = new JsonFileStore<JsonStoreTestEntity>(
            path, entity => entity.Id);

        await firstStore.AddAsync(new JsonStoreTestEntity("E002", "Persisted"));

        var secondStore = new JsonFileStore<JsonStoreTestEntity>(
            path, entity => entity.Id);

        var result = await secondStore.GetByKeyAsync("E002");

        Assert.NotNull(result);
        Assert.Equal("Persisted", result.Name);
    }

    [Fact]
    public async Task UpdateAsync_PersistsUpdatedEntity()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var store = new JsonFileStore<JsonStoreTestEntity>(
            Path.Combine(directory.Path, "entities.json"),
            entity => entity.Id);

        await store.AddAsync(new JsonStoreTestEntity("E003", "Before"));

        await store.UpdateAsync(new JsonStoreTestEntity("E003", "After"));

        var result = await store.GetByKeyAsync("E003");

        Assert.NotNull(result);
        Assert.Equal("After", result.Name);
    }

    [Fact]
    public async Task UpdateAsync_WhenKeyDoesNotExist_ThrowsKeyNotFoundException()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var store = new JsonFileStore<JsonStoreTestEntity>(
            Path.Combine(directory.Path, "entities.json"),
            entity => entity.Id);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.UpdateAsync(new JsonStoreTestEntity("MISSING", "Value")));
    }
}

public sealed record JsonStoreTestEntity(string Id, string Name);
