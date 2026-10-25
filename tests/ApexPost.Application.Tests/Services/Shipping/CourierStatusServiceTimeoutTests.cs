using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Shipping;
using ApexPost.Domain.Entities;
using Xunit;

namespace ApexPost.Application.Tests.Services.Shipping;

public sealed class CourierStatusServiceTimeoutTests
{
    [Fact]
    public async Task UpdateAsync_SimulatedCourierDelayExceedsTimeout_ReturnsTimedOut()
    {
        var service = new CourierStatusService(
            new EmptyShipmentRepository(),
            TimeProvider.System,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(200));

        var result = await service.UpdateAsync("SHP-1", "In transit");

        Assert.Equal(CourierStatusUpdateStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_RequestCancellationRemainsCancellation()
    {
        var service = new CourierStatusService(
            new EmptyShipmentRepository(),
            TimeProvider.System,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.UpdateAsync("SHP-1", "In transit", cancellation.Token));
    }

    private sealed class EmptyShipmentRepository : IShipmentRepository
    {
        public Task<Shipment?> GetByIdAsync(string shipmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Shipment?>(null);

        public Task<IReadOnlyList<Shipment>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Shipment>>([]);

        public Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(Shipment shipment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string shipmentId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
