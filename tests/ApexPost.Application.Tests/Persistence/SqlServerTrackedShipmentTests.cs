using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

/// <summary>
/// Opt-in SQL Server tests for tracked shipments: they prove what the SQLite tests in ApexPost.IntegrationTests cannot,
/// namely that the REAL unique index on SQL Server rejects a duplicate tracking number and that SqlUnitOfWork maps
/// SQL Server error 2601/2627 to UniqueKeyConflictException (the exception the API turns into HTTP 409).
/// Skipped unless APEXPOST_SQL_TEST_CONNECTION is set (see the README); the fixture creates and drops a private
/// ApexPost_SqlIT_* database, so no existing database is touched.
/// </summary>
[Collection("SQL Server Integration")]
public sealed class SqlServerTrackedShipmentTests(SqlServerIntegrationTestFixture fixture)
{
    private sealed class SqlServerOptInFactAttribute : FactAttribute
    {
        public SqlServerOptInFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APEXPOST_SQL_TEST_CONNECTION")))
            {
                Skip = "Set APEXPOST_SQL_TEST_CONNECTION to run the isolated SQL Server integration suite.";
            }
        }
    }

    private ApexPostDbContext CreateContext() => new(new DbContextOptionsBuilder<ApexPostDbContext>()
        .UseSqlServer(fixture.ConnectionString, sql => sql.EnableRetryOnFailure())
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
        .Options);

    private static TrackedShipment NewShipment(string tracking) => TrackedShipment.Create(
        tracking, 2.5m, "Jane Example", "+15550100", "1 Example Street", "Springfield",
        createdAtUtc: new DateTimeOffset(2025, 3, 10, 8, 0, 0, TimeSpan.Zero));

    [SqlServerOptInFact]
    public async Task SaveChanges_DuplicateTrackingNumber_ThrowsUniqueKeyConflictException()
    {
        // Arrange: a unique tracking number per run, the database itself is private to this run.
        var tracking = $"APX-{Random.Shared.Next(10_000_000, 99_999_999)}";
        await using (var first = CreateContext())
        {
            await new SqlTrackedShipmentRepository(first).AddAsync(NewShipment(tracking));
            await new SqlUnitOfWork(first).SaveChangesAsync();
        }

        await using var second = CreateContext();
        await new SqlTrackedShipmentRepository(second).AddAsync(NewShipment(tracking));

        // Act + Assert
        await Assert.ThrowsAsync<UniqueKeyConflictException>(() => new SqlUnitOfWork(second).SaveChangesAsync());
    }

    [SqlServerOptInFact]
    public async Task AddAsync_ThenGetByTrackingNumber_RoundTripsOnSqlServer()
    {
        // Arrange
        var tracking = $"APX-{Random.Shared.Next(10_000_000, 99_999_999)}";
        await using (var write = CreateContext())
        {
            await new SqlTrackedShipmentRepository(write).AddAsync(NewShipment(tracking));
            await new SqlUnitOfWork(write).SaveChangesAsync();
        }

        // Act
        await using var read = CreateContext();
        var loaded = await new SqlTrackedShipmentRepository(read).GetByTrackingNumberAsync(tracking);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(2.5m, loaded.WeightKg);
        Assert.Equal(0m, loaded.Surcharge);
    }
}
