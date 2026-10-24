
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

public sealed class JsonShipmentRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_WhenMissing_ReturnsNull()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonShipmentRepository(directory.CreateOptions());

        var result = await repository.GetByIdAsync("SHP001");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByBranchAsync_WhenEmpty_ReturnsEmptyCollection()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonShipmentRepository(directory.CreateOptions());

        var result = await repository.GetByBranchAsync("BR001");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonShipmentRepository(directory.CreateOptions());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetByIdAsync("SHP001", cancellation.Token));
    }

    [Fact]
    public async Task CourierStatusSurvivesRepositoryReload()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonShipmentRepository(directory.CreateOptions());
        var receivedAt = DateTimeOffset.Parse("2026-10-24T10:30:00Z");
        var shipment = new Shipment(
            "SHP-STATUS-1",
            "Parcel",
            new Weight(1m),
            10m,
            10m,
            10m,
            "ZONE1",
            ShippingServiceLevel.Standard);
        shipment.RecordCourierStatus("In transit", receivedAt);

        await repository.AddAsync(shipment);
        var reloaded = await new JsonShipmentRepository(directory.CreateOptions())
            .GetByIdAsync("SHP-STATUS-1");

        Assert.NotNull(reloaded);
        Assert.Equal("In transit", reloaded.CourierStatus);
        Assert.Equal(receivedAt, reloaded.CourierStatusUpdatedAtUtc);
    }

    [Fact]
    public async Task ShipmentBranchAndTransactionReferencesSurviveRepositoryReload()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonShipmentRepository(directory.CreateOptions());
        await repository.AddAsync(new Shipment(
            "SHP-SALE-1",
            "Parcel",
            new Weight(1m),
            10m,
            10m,
            10m,
            "ZONE1",
            ShippingServiceLevel.Standard,
            new BranchCode("BR001"),
            "TXN-SALE-1"));

        var shipment = await new JsonShipmentRepository(directory.CreateOptions())
            .GetByIdAsync("SHP-SALE-1");
        var branchShipments = await new JsonShipmentRepository(directory.CreateOptions())
            .GetByBranchAsync("BR001");

        Assert.NotNull(shipment);
        Assert.Equal("BR001", shipment.BranchCode!.Value);
        Assert.Equal("TXN-SALE-1", shipment.CounterTransactionId);
        Assert.Single(branchShipments);
    }
}
