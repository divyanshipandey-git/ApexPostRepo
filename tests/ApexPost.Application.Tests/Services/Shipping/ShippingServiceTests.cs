
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Shipping;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Application.Tests.Services.Shipping;

public sealed class ShippingServiceTests
{
    [Fact]
    public void Constructor_WhenRepositoryIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ShippingService(
                null!,
                Array.Empty<ShippingPricingRule>()));
    }

    [Fact]
    public void Constructor_WhenPricingRulesAreNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ShippingService(
                new FakeShipmentRepository(),
                null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumWeightIsNotPositive_Throws(
        decimal maximumWeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShippingService(
                new FakeShipmentRepository(),
                Array.Empty<ShippingPricingRule>(),
                maximumWeight));
    }

    [Fact]
    public void CalculatePrice_WhenZoneHasNoRule_Throws()
    {
        var service = CreateService();

        Assert.Throws<InvalidOperationException>(() =>
            service.CalculatePrice(
                "UNKNOWN",
                ShippingServiceLevel.Standard,
                new Weight(1m)));
    }

    [Fact]
    public void CalculatePrice_WhenWeightExceedsLimit_Throws()
    {
        var service = CreateService(maximumWeightKg: 5m);

        Assert.Throws<InvalidOperationException>(() =>
            service.CalculatePrice(
                "ZONE1",
                ShippingServiceLevel.Standard,
                new Weight(6m)));
    }

    [Theory]
    [InlineData(ShippingServiceLevel.Standard, 30)]
    [InlineData(ShippingServiceLevel.Express, 20)]
    [InlineData(ShippingServiceLevel.Registered, 10)]
    public void CalculatePrice_ServiceLevelLimit_AllowsBoundaryAndRejectsAbove(
        ShippingServiceLevel level,
        decimal maximumWeight)
    {
        var service = CreateServiceWithServiceLevelLimits();

        Assert.Equal(maximumWeight + 50m,
            service.CalculatePrice("ZONE1", level, new Weight(maximumWeight)).Amount);
        Assert.Throws<InvalidOperationException>(() =>
            service.CalculatePrice("ZONE1", level, new Weight(maximumWeight + 0.01m)));
    }

    [Fact]
    public void CalculatePrice_WhenDestinationZoneIsBlank_Throws()
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() =>
            service.CalculatePrice(
                " ",
                ShippingServiceLevel.Standard,
                new Weight(1m)));
    }

    [Fact]
    public async Task CreateShipmentAsync_WhenCancelled_Throws()
    {
        var service = CreateService();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CreateShipmentAsync(
                "SHIP001",
                "Parcel",
                1m,
                10m,
                10m,
                10m,
                "ZONE1",
                ShippingServiceLevel.Standard,
                cancellation.Token));
    }

    [Fact]
    public async Task CreateShipmentAsync_WhenZoneHasNoRule_ThrowsAndDoesNotSave()
    {
        var repository = new FakeShipmentRepository();
        var service = new ShippingService(
            repository,
            Array.Empty<ShippingPricingRule>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateShipmentAsync(
                "SHIP001",
                "Parcel",
                1m,
                10m,
                10m,
                10m,
                "ZONE1",
                ShippingServiceLevel.Standard));

        Assert.Null(repository.AddedShipment);
    }

    private static ShippingService CreateService(
        decimal maximumWeightKg = 30m)
    {
        var rule = new ShippingPricingRule(
            "ZONE1",
            ShippingServiceLevel.Standard,
            1m,
            new Money(50m));

        return new ShippingService(
            new FakeShipmentRepository(),
            new[] { rule },
            maximumWeightKg);
    }

    private static ShippingService CreateServiceWithServiceLevelLimits()
    {
        var rules = Enum.GetValues<ShippingServiceLevel>()
            .Select(level => new ShippingPricingRule(
                "ZONE1", level, 1m, new Money(50m)))
            .ToArray();
        var limits = new Dictionary<ShippingServiceLevel, decimal>
        {
            [ShippingServiceLevel.Standard] = 30m,
            [ShippingServiceLevel.Express] = 20m,
            [ShippingServiceLevel.Registered] = 10m
        };

        return new ShippingService(new FakeShipmentRepository(), rules,
            maximumWeightKgByServiceLevel: limits);
    }

    private sealed class FakeShipmentRepository : IShipmentRepository
    {
        public Shipment? AddedShipment { get; private set; }

        public Task<Shipment?> GetByIdAsync(
            string shipmentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Shipment?>(null);

        public Task<IReadOnlyList<Shipment>> GetByBranchAsync(
            string branchCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Shipment>>(
                Array.Empty<Shipment>());

        public Task AddAsync(
            Shipment shipment,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddedShipment = shipment;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            Shipment shipment,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(
            string shipmentId,
            CancellationToken cancellationToken = default)
        {
            if (AddedShipment?.Id == shipmentId)
            {
                AddedShipment = null;
            }

            return Task.CompletedTask;
        }
    }
}
