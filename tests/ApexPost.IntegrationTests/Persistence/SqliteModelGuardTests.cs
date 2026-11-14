using ApexPost.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApexPost.IntegrationTests.Persistence;

/// <summary>
/// Guards the limits of the SQLite substitute (TESTING-STRATEGY.md section 6). The production model contains SQL Server
/// only mapping (rowversion, datetime2, server defaults, T-SQL check constraints). The SQLite customizer removes it; these
/// tests make that visible and fail when the production model changes in a way the customizer no longer covers, instead of
/// the repository tests silently running against a model that differs from production.
/// </summary>
public sealed class SqliteModelGuardTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public void ProductionModel_StillContainsSqlServerOnlyMapping_SoTheSqliteSubstituteIsLimited()
    {
        // Arrange: a context WITHOUT the customizer, only to read the model (nothing is created).
        var options = new DbContextOptionsBuilder<ApexPost.Infrastructure.Persistence.ApexPostDbContext>()
            .UseSqlServer("Server=never-contacted.invalid;Database=none").Options;
        using var production = new ApexPost.Infrastructure.Persistence.ApexPostDbContext(options);

        // Act
        var propertiesWithSqlServerTypes = production.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Count(p => !string.IsNullOrEmpty(p.GetColumnType()) || p.IsConcurrencyToken);

        // Assert: if this becomes 0 the SQLite customizer is no longer needed and the strategy text must be updated.
        Assert.True(propertiesWithSqlServerTypes > 0);
    }

    [Fact]
    public void SqliteDatabase_IsCreatedWithTheTrackedShipmentTablesAndUniqueIndex()
    {
        // Arrange
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT name, sql FROM sqlite_master WHERE name IN ('TrackedShipments', 'TrackedShipmentScans', 'IX_TrackedShipments_TrackingNumber')";

        // Act
        var found = new Dictionary<string, string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                found[reader.GetString(0)] = reader.GetString(1);
            }
        }

        // Assert: the schema the repository tests run on really has both tables and a UNIQUE tracking number index.
        Assert.Equal(3, found.Count);
        Assert.Contains("UNIQUE", found["IX_TrackedShipments_TrackingNumber"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqliteModel_KeepsTheUniqueTrackingNumberIndexOfProduction()
    {
        // Arrange
        using var context = _database.CreateContext();

        // Act
        var shipment = context.Model.FindEntityType(typeof(ApexPost.Domain.Entities.TrackedShipment))!;
        var unique = shipment.GetIndexes().Single(i => i.Properties.Single().Name == "TrackingNumber");

        // Assert: the rule the duplicate-tracking-number tests depend on is part of the substitute model.
        Assert.True(unique.IsUnique);
    }
}
