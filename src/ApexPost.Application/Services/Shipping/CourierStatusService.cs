using ApexPost.Application.Abstractions.Repositories;

namespace ApexPost.Application.Services.Shipping;

public sealed class CourierStatusService
{
    private static readonly SemaphoreSlim TrackingLookupGate = new(4, 4);
    private readonly IShipmentRepository _shipmentRepository;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _lookupTimeout;
    private readonly TimeSpan _simulatedLookupDelay;

    public CourierStatusService(
        IShipmentRepository shipmentRepository,
        TimeProvider timeProvider,
        TimeSpan? lookupTimeout = null,
        TimeSpan? simulatedLookupDelay = null)
    {
        ArgumentNullException.ThrowIfNull(shipmentRepository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _shipmentRepository = shipmentRepository;
        _timeProvider = timeProvider;
        _lookupTimeout = lookupTimeout ?? TimeSpan.FromSeconds(2);
        _simulatedLookupDelay = simulatedLookupDelay ?? TimeSpan.Zero;
        if (_lookupTimeout <= TimeSpan.Zero || _simulatedLookupDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lookupTimeout));
        }
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
        using var timeoutSource = new CancellationTokenSource(_lookupTimeout, _timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        var gateEntered = false;
        try
        {
            await TrackingLookupGate.WaitAsync(linkedSource.Token);
            gateEntered = true;
            if (_simulatedLookupDelay > TimeSpan.Zero)
            {
                await Task.Delay(_simulatedLookupDelay, _timeProvider, linkedSource.Token);
            }

            var shipment = await _shipmentRepository.GetByIdAsync(shipmentId, linkedSource.Token);
            if (shipment is null)
            {
                return new CourierStatusUpdateResult(CourierStatusUpdateStatus.ShipmentNotFound);
            }

            var receivedAtUtc = _timeProvider.GetUtcNow();
            var normalizedStatus = status!.Trim();
            shipment.RecordCourierStatus(normalizedStatus, receivedAtUtc);
            await _shipmentRepository.UpdateAsync(shipment, linkedSource.Token);

            return new CourierStatusUpdateResult(
                CourierStatusUpdateStatus.Updated,
                shipment.Id,
                shipment.CourierStatus,
                receivedAtUtc);
        }
        // A partner/client cancellation is not a failed courier lookup; only the service-owned deadline maps to 504.
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            return new CourierStatusUpdateResult(CourierStatusUpdateStatus.TimedOut);
        }
        finally
        {
            if (gateEntered)
            {
                TrackingLookupGate.Release();
            }
        }
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
