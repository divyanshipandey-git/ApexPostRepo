using ApexPost.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ApexPost.IntegrationTests.TestSupport;

/// <summary>
/// One private in-memory SQLite database per instance (so one per test: no shared state, no ordering dependence).
/// The connection stays open for the lifetime of the object because an in-memory database disappears with it.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public SqliteConnection Connection => _connection;

    public ApexPostDbContext CreateContext() => new(BuildOptions(_connection));

    internal static DbContextOptions<ApexPostDbContext> BuildOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<ApexPostDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteModelCustomizer>()
            .Options;

    public void Dispose() => _connection.Dispose();
}
