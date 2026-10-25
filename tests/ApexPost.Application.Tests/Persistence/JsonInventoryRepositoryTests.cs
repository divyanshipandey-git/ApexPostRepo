
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
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

    [Fact]
    public async Task TryReserveAsync_ReservesAllRequestedStockAtomically()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonInventoryRepository(directory.CreateOptions());
        await repository.AddAsync(new InventoryStock(new BranchCode("BR001"), new Sku("ENV001"), 3));
        await repository.AddAsync(new InventoryStock(new BranchCode("BR001"), new Sku("ENV002"), 1));

        var result = await repository.TryReserveAsync("BR001", new Dictionary<string, int>
        {
            ["ENV001"] = 1,
            ["ENV002"] = 2
        });

        Assert.Equal(InventoryReservationStatus.StockUnavailable, result.Status);
        Assert.Null(result.Reservation);
        Assert.Equal(3, (await repository.GetByBranchAndSkuAsync("BR001", "ENV001"))!.Quantity);
        Assert.Equal(1, (await repository.GetByBranchAndSkuAsync("BR001", "ENV002"))!.Quantity);
    }

    [Fact]
    public async Task ConcurrentReservationsCannotOversellAcrossRepositoryInstances()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var options = directory.CreateOptions();
        var first = new JsonInventoryRepository(options);
        var second = new JsonInventoryRepository(options);
        await first.AddAsync(new InventoryStock(new BranchCode("BR001"), new Sku("ENV001"), 1));

        var results = await Task.WhenAll(
            first.TryReserveAsync("BR001", new Dictionary<string, int> { ["ENV001"] = 1 }),
            second.TryReserveAsync("BR001", new Dictionary<string, int> { ["ENV001"] = 1 }));

        Assert.Single(results, result => result.Status == InventoryReservationStatus.Reserved);
        Assert.Single(results, result => result.Status == InventoryReservationStatus.StockUnavailable);
        Assert.Equal(0, (await first.GetByBranchAndSkuAsync("BR001", "ENV001"))!.Quantity);
    }

    [Theory]
    [MemberData(nameof(InvalidQuantities))]
    public async Task TryReserveAsync_RejectsEmptyOrMalformedQuantities(
        IReadOnlyDictionary<string, int> quantities)
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonInventoryRepository(directory.CreateOptions());

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.TryReserveAsync("BR001", quantities));
    }

    public static TheoryData<IReadOnlyDictionary<string, int>> InvalidQuantities => new()
    {
        new Dictionary<string, int>(),
        new Dictionary<string, int> { ["ENV001"] = 0 },
        new Dictionary<string, int> { ["ENV001"] = -1 },
        new Dictionary<string, int> { [" "] = 1 }
    };

    [Fact]
    public async Task RestoreReservationAsync_RestoresOnlyItsReservationAndOnlyOnce()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonInventoryRepository(directory.CreateOptions());
        await repository.AddAsync(new InventoryStock(new BranchCode("BR001"), new Sku("ENV001"), 5));

        var reserved = await repository.TryReserveAsync(
            "BR001",
            new Dictionary<string, int> { ["ENV001"] = 2 });
        var reservation = Assert.IsType<InventoryReservation>(reserved.Reservation);
        Assert.Equal(3, (await repository.GetByBranchAndSkuAsync("BR001", "ENV001"))!.Quantity);

        await repository.RestoreReservationAsync(reservation);

        Assert.Equal(5, (await repository.GetByBranchAndSkuAsync("BR001", "ENV001"))!.Quantity);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.RestoreReservationAsync(reservation));
        Assert.Equal(5, (await repository.GetByBranchAndSkuAsync("BR001", "ENV001"))!.Quantity);
    }

    [Fact]
    public async Task RestoreReservationAsync_RejectsUnknownOrModifiedReservation()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonInventoryRepository(directory.CreateOptions());
        await repository.AddAsync(new InventoryStock(new BranchCode("BR001"), new Sku("ENV001"), 5));
        var reserved = await repository.TryReserveAsync(
            "BR001",
            new Dictionary<string, int> { ["ENV001"] = 2 });
        var reservation = Assert.IsType<InventoryReservation>(reserved.Reservation);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RestoreReservationAsync(
            reservation with
            {
                SkuQuantities = new Dictionary<string, int> { ["ENV001"] = 5 }
            }));
        Assert.Equal(3, (await repository.GetByBranchAndSkuAsync("BR001", "ENV001"))!.Quantity);
    }

    [Fact]
    public async Task RestoreReservationAsync_RejectsEmptyOrMalformedQuantities()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonInventoryRepository(directory.CreateOptions());
        var empty = new InventoryReservation(
            Guid.NewGuid(),
            "BR001",
            new Dictionary<string, int>());
        var malformed = new InventoryReservation(
            Guid.NewGuid(),
            "BR001",
            new Dictionary<string, int> { ["ENV001"] = 0 });

        await Assert.ThrowsAsync<ArgumentException>(() => repository.RestoreReservationAsync(empty));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.RestoreReservationAsync(malformed));
    }

    [Fact]
    public async Task RestoreReservationAsync_CannotRestoreAReservationThatNeverSucceeded()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonInventoryRepository(directory.CreateOptions());
        await repository.AddAsync(new InventoryStock(new BranchCode("BR001"), new Sku("ENV001"), 1));

        var failed = await repository.TryReserveAsync(
            "BR001",
            new Dictionary<string, int> { ["ENV001"] = 2 });
        Assert.Equal(InventoryReservationStatus.StockUnavailable, failed.Status);
        var fabricated = new InventoryReservation(
            Guid.NewGuid(),
            "BR001",
            new Dictionary<string, int> { ["ENV001"] = 2 });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.RestoreReservationAsync(fabricated));
        Assert.Equal(1, (await repository.GetByBranchAndSkuAsync("BR001", "ENV001"))!.Quantity);
    }

    [Fact]
    public async Task CommitReservationAsync_PreventsLaterRestoration()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonInventoryRepository(directory.CreateOptions());
        await repository.AddAsync(new InventoryStock(new BranchCode("BR001"), new Sku("ENV001"), 5));
        var reserved = await repository.TryReserveAsync(
            "BR001",
            new Dictionary<string, int> { ["ENV001"] = 2 });
        var reservation = Assert.IsType<InventoryReservation>(reserved.Reservation);

        await repository.CommitReservationAsync(reservation);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.RestoreReservationAsync(reservation));
        Assert.Equal(3, (await repository.GetByBranchAndSkuAsync("BR001", "ENV001"))!.Quantity);
    }
}
