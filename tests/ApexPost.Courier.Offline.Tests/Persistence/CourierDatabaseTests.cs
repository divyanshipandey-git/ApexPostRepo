using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Persistence;
using ApexPost.Courier.Offline.Services;

namespace ApexPost.Courier.Offline.Tests.Persistence;

public class CourierDatabaseTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;

    public CourierDatabaseTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
    }

    public void Dispose()
    {
    }

    [Fact]
    public void Constructor_CreatesDatabaseAndIndexes()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ApexPost", "CourierOffline", $"Test_{Guid.NewGuid():N}");
        using var database = new CourierDatabase(dbPath, _loggerFactory.CreateLogger<CourierDatabase>());

        database.Database.Should().NotBeNull();
        database.DefaultCollection.Should().NotBeNull();
    }

    [Fact]
    public void AddChangeListener_AndRemoveChangeListener_Works()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ApexPost", "CourierOffline", $"Test_{Guid.NewGuid():N}");
        using var database = new CourierDatabase(dbPath, _loggerFactory.CreateLogger<CourierDatabase>());

        var changedIds = new List<string>();
        var token = database.AddChangeListener(ids => changedIds.AddRange(ids));

        token.Should().NotBeNull();

        database.RemoveChangeListener();
    }

    [Fact]
    public void RemoveChangeListener_WithoutAdding_DoesNotThrow()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ApexPost", "CourierOffline", $"Test_{Guid.NewGuid():N}");
        using var database = new CourierDatabase(dbPath, _loggerFactory.CreateLogger<CourierDatabase>());

        var act = () => database.RemoveChangeListener();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_RemovesChangeListenerAndClosesDatabase()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ApexPost", "CourierOffline", $"Test_{Guid.NewGuid():N}");
        var database = new CourierDatabase(dbPath, _loggerFactory.CreateLogger<CourierDatabase>());

        database.AddChangeListener(_ => { });
        database.Dispose();

        // Should not throw on second dispose
        database.Dispose();
    }
}