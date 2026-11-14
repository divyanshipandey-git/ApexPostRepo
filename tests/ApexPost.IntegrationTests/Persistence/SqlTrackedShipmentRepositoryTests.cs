using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApexPost.IntegrationTests.Persistence;

/// <summary>
/// The real SqlTrackedShipmentRepository and SqlUnitOfWork on a real EF Core DbContext over in-memory SQLite.
/// Each test owns a private database. Every assertion reads through a NEW context so nothing is satisfied by the
/// change tracker cache. Not proven here: SQL Server specifics (see TESTING-STRATEGY.md, section 6).
/// </summary>
public sealed class SqlTrackedShipmentRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2025, 3, 10, 8, 0, 0, TimeSpan.Zero);
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static TrackedShipment Shipment(string tracking, DateTimeOffset? createdAt = null, string city = "Springfield", decimal weight = 2.5m) =>
        TrackedShipment.Create(tracking, weight, "Jane Example", "+15550100", "1 Example Street", city, createdAtUtc: createdAt ?? T0);

    private async Task SaveAsync(params TrackedShipment[] shipments)
    {
        await using var context = _database.CreateContext();
        var repository = new SqlTrackedShipmentRepository(context);
        foreach (var shipment in shipments)
        {
            await repository.AddAsync(shipment);
        }

        await new SqlUnitOfWork(context).SaveChangesAsync();
    }

    private static TrackedShipmentScan ScanOf(TrackedShipment shipment, string id) => new()
    {
        Id = id,
        TrackedShipmentId = shipment.Id,
        ScanType = ScanType.Pickup,
        CourierId = "COURIER-001",
        Location = "Depot A",
        ScannedAtUtc = T0.AddHours(1)
    };

    [Fact]
    public async Task AddAsync_ThenGetByTrackingNumber_RoundTripsAllFields()
    {
        // Arrange
        var shipment = TrackedShipment.Create("APX-20250101", 12.34m, "Jane Example", "+15550100", "1 Example Street",
            "Springfield", "12345", "US", "Leave at door", 99.5m, T0);

        // Act
        await SaveAsync(shipment);
        await using var context = _database.CreateContext();
        var loaded = await new SqlTrackedShipmentRepository(context).GetByTrackingNumberAsync("APX-20250101");

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(shipment.Id, loaded.Id);
        Assert.Equal(12.34m, loaded.WeightKg);
        Assert.Equal("Springfield", loaded.RecipientCity);
        Assert.Equal("Leave at door", loaded.SpecialInstructions);
        Assert.Equal(99.5m, loaded.DeclaredValue);
        Assert.Equal(ShipmentStatus.Created, loaded.Status);
        Assert.Equal(T0, loaded.CreatedAtUtc);
    }

    [Fact]
    public async Task GetByTrackingNumberAsync_UnknownNumber_ReturnsNull()
    {
        // Arrange
        await SaveAsync(Shipment("APX-20250101"));
        await using var context = _database.CreateContext();

        // Act
        var loaded = await new SqlTrackedShipmentRepository(context).GetByTrackingNumberAsync("APX-20259999");

        // Assert
        Assert.Null(loaded);
    }

    [Fact]
    public async Task SaveChanges_SameTrackingNumberTwice_IsRejectedByTheUniqueIndex()
    {
        // Arrange
        await SaveAsync(Shipment("APX-20250101"));
        await using var context = _database.CreateContext();
        var repository = new SqlTrackedShipmentRepository(context);
        await repository.AddAsync(Shipment("APX-20250101"));

        // Act + Assert: on SQL Server SqlUnitOfWork maps this to UniqueKeyConflictException (error 2601/2627);
        // SQLite raises a different error code, so here we only prove that the database itself enforces uniqueness.
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => new SqlUnitOfWork(context).SaveChangesAsync());
    }

    [Fact]
    public async Task UpdateAsync_StatusChange_IsPersistedWithTheNewScanInHistory()
    {
        // Arrange
        await SaveAsync(Shipment("APX-20250101"));
        await using (var context = _database.CreateContext())
        {
            var repository = new SqlTrackedShipmentRepository(context);
            var tracked = await repository.GetByTrackingNumberWithTrackingAsync("APX-20250101");
            tracked!.UpdateStatus(ShipmentStatus.PickedUp);

            // Act
            await repository.UpdateAsync(tracked);
            await new SqlUnitOfWork(context).SaveChangesAsync();
        }

        // Assert
        await using var verify = _database.CreateContext();
        var loaded = await new SqlTrackedShipmentRepository(verify).GetByTrackingNumberAsync("APX-20250101");
        Assert.Equal(ShipmentStatus.PickedUp, loaded!.Status);
        Assert.Equal(ScanType.Pickup, Assert.Single(loaded.Scans).ScanType);
    }

    [Fact]
    public async Task GetScansByIdsAsync_ReturnsOnlyStoredScanIds()
    {
        // Arrange
        var shipment = Shipment("APX-20250101");
        shipment.AddScan(ScanOf(shipment, "scan::a"));
        await SaveAsync(shipment);
        await using var context = _database.CreateContext();

        // Act
        var found = await new SqlTrackedShipmentRepository(context).GetScansByIdsAsync(["scan::a", "scan::missing"]);

        // Assert
        Assert.Equal("scan::a", Assert.Single(found).Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesShipmentAndCascadesToItsScans()
    {
        // Arrange
        var shipment = Shipment("APX-20250101");
        shipment.AddScan(ScanOf(shipment, "scan::a"));
        await SaveAsync(shipment);

        // Act
        await using (var context = _database.CreateContext())
        {
            var repository = new SqlTrackedShipmentRepository(context);
            var tracked = await repository.GetByTrackingNumberWithTrackingAsync("APX-20250101");
            await repository.DeleteAsync(tracked!);
            await new SqlUnitOfWork(context).SaveChangesAsync();
        }

        // Assert
        await using var verify = _database.CreateContext();
        Assert.Empty(await verify.TrackedShipments.ToListAsync());
        Assert.Empty(await verify.TrackedShipmentScans.ToListAsync());
    }

    [Fact]
    public async Task Query_FilterOrderAndPage_ReturnsTheExpectedSlice()
    {
        // Arrange: five shipments, one day apart; two of them in Shelbyville.
        await SaveAsync(
            Shipment("APX-20250101", T0.AddDays(0), "Springfield"),
            Shipment("APX-20250102", T0.AddDays(1), "Shelbyville"),
            Shipment("APX-20250103", T0.AddDays(2), "Springfield"),
            Shipment("APX-20250104", T0.AddDays(3), "Shelbyville"),
            Shipment("APX-20250105", T0.AddDays(4), "Springfield"));
        await using var context = _database.CreateContext();
        var repository = new SqlTrackedShipmentRepository(context);

        // Act
        var page = await repository.Query()
            .Where(s => s.RecipientCity == "Springfield")
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip(1).Take(1)
            .Select(s => s.TrackingNumber)
            .ToListAsync();
        var total = await repository.Query().CountAsync(s => s.RecipientCity == "Shelbyville");

        // Assert: newest first is 05, 03, 01; page 2 of size 1 is 03.
        Assert.Equal(["APX-20250103"], page);
        Assert.Equal(2, total);
    }

    [Fact]
    public async Task Query_FilterByCreatedRange_UsesTheStoredTimestamps()
    {
        // Arrange
        await SaveAsync(
            Shipment("APX-20250101", T0),
            Shipment("APX-20250102", T0.AddDays(5)),
            Shipment("APX-20250103", T0.AddDays(10)));
        await using var context = _database.CreateContext();

        // Act
        var inRange = await new SqlTrackedShipmentRepository(context).Query()
            .Where(s => s.CreatedAtUtc >= T0.AddDays(1) && s.CreatedAtUtc <= T0.AddDays(9))
            .Select(s => s.TrackingNumber)
            .ToListAsync();

        // Assert
        Assert.Equal(["APX-20250102"], inRange);
    }
}
