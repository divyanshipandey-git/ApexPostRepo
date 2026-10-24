using ApexPost.Application.Abstractions.Repositories;

namespace ApexPost.Application.Services.Shipping;

public sealed class CourierStatusService
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly TimeProvider _timeProvider;

    public CourierStatusService(IShipmentRepository shipmentRepository, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(shipmentRepository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _shipmentRepository = shipmentRepository;
        _timeProvider = timeProvider;
    }

    public async Task<CourierStatusUpdateResult> UpdateAsync(
        string shipmentId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shipmentId))
        {
            return new CourierStatusUpdateResult(CourierStatusUpdateStatus.InvalidShipmentId);
        }

        if (shipmentId.Length > 128 || shipmentId.Any(char.IsControl))
        {
            return new CourierStatusUpdateResult(CourierStatusUpdateStatus.InvalidShipmentId);
        }

        if (!IsValidStatus(status))
        {
            return new CourierStatusUpdateResult(CourierStatusUpdateStatus.InvalidStatus);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var shipment = await _shipmentRepository.GetByIdAsync(shipmentId, cancellationToken);
        if (shipment is null)
        {
            return new CourierStatusUpdateResult(CourierStatusUpdateStatus.ShipmentNotFound);
        }

        var receivedAtUtc = _timeProvider.GetUtcNow();
        var normalizedStatus = status!.Trim();
        shipment.RecordCourierStatus(normalizedStatus, receivedAtUtc);
        await _shipmentRepository.UpdateAsync(shipment, cancellationToken);

        return new CourierStatusUpdateResult(
            CourierStatusUpdateStatus.Updated,
            shipment.Id,
            shipment.CourierStatus,
            receivedAtUtc);
    }

    private static bool IsValidStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var normalized = status.Trim();
        return normalized.Length <= 100 && !normalized.Any(char.IsControl);
    }
}
