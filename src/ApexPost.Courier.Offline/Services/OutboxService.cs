using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;

namespace ApexPost.Courier.Offline.Services;

/// <summary>
/// Interface for uploading scans to the server.
/// </summary>
public interface IShipmentUploader
{
    /// <summary>
    /// Uploads a scan to the server.
    /// </summary>
    /// <param name="scan">The scan to upload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if upload succeeded, false otherwise.</returns>
    Task<bool> UploadAsync(ShipmentScan scan, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fake implementation of IShipmentUploader for testing.
/// Simulates successful/failed uploads.
/// </summary>
public sealed class FakeShipmentUploader : IShipmentUploader
{
    private readonly Random _random = new();
    private readonly double _failureRate;

    public FakeShipmentUploader(double failureRate = 0.1)
    {
        _failureRate = Math.Clamp(failureRate, 0, 1);
    }

    public Task<bool> UploadAsync(ShipmentScan scan, CancellationToken cancellationToken = default)
    {
        // Simulate network delay
        return Task.FromResult(_random.NextDouble() > _failureRate);
    }
}

/// <summary>
/// Service that manages the outbox pattern for offline sync.
/// Reads pending scans and marks them Synced or Failed after upload attempts.
/// </summary>
public sealed class OutboxService
{
    private readonly IScanRepository _scanRepository;
    private readonly IShipmentUploader _uploader;

    public OutboxService(IScanRepository scanRepository, IShipmentUploader uploader)
    {
        _scanRepository = scanRepository ?? throw new ArgumentNullException(nameof(scanRepository));
        _uploader = uploader ?? throw new ArgumentNullException(nameof(uploader));
    }

    /// <summary>
    /// Processes all pending scans by attempting to upload them.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of scans processed.</returns>
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pendingScans = await _scanRepository.GetBySyncStatusAsync(SyncStatus.Pending, cancellationToken);
        var processed = 0;

        foreach (var scan in pendingScans)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var success = await _uploader.UploadAsync(scan, cancellationToken);
                scan.SyncStatus = success ? SyncStatus.Synced : SyncStatus.Failed;
                await _scanRepository.UpdateAsync(scan, cancellationToken);
                processed++;
            }
            catch (Exception)
            {
                scan.SyncStatus = SyncStatus.Failed;
                await _scanRepository.UpdateAsync(scan, cancellationToken);
                processed++;
            }
        }

        return processed;
    }
}