using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using Microsoft.Extensions.Logging;

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
/// Reads Pending scans and Failed scans (so a failed upload is retried on the next run)
/// and marks them Synced or Failed after each upload attempt.
/// </summary>
public sealed class OutboxService
{
    private readonly IScanRepository _scanRepository;
    private readonly IShipmentUploader _uploader;
    private readonly ILogger<OutboxService>? _logger;

    public OutboxService(IScanRepository scanRepository, IShipmentUploader uploader, ILogger<OutboxService>? logger = null)
    {
        _scanRepository = scanRepository ?? throw new ArgumentNullException(nameof(scanRepository));
        _uploader = uploader ?? throw new ArgumentNullException(nameof(uploader));
        _logger = logger;
    }

    /// <summary>
    /// Processes all Pending scans and re-tries all Failed scans.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of scans processed (upload attempted).</returns>
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pendingScans = await _scanRepository.GetBySyncStatusAsync(SyncStatus.Pending, cancellationToken);
        var failedScans = await _scanRepository.GetBySyncStatusAsync(SyncStatus.Failed, cancellationToken);
        var processed = 0;

        // Pending first (oldest work), then previously failed scans being retried.
        foreach (var scan in pendingScans.Concat(failedScans))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var wasRetry = scan.SyncStatus == SyncStatus.Failed;

            try
            {
                var success = await _uploader.UploadAsync(scan, cancellationToken);
                scan.SyncStatus = success ? SyncStatus.Synced : SyncStatus.Failed;
                _logger?.LogInformation(
                    "Scan {ScanId} {Outcome}{Retry}",
                    scan.Id, success ? "synced" : "failed to upload", wasRetry ? " (retry)" : string.Empty);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                scan.SyncStatus = SyncStatus.Failed;
                _logger?.LogWarning(ex, "Upload of scan {ScanId} threw; it is marked Failed and will be retried", scan.Id);
            }

            await _scanRepository.UpdateAsync(scan, cancellationToken);
            processed++;
        }

        return processed;
    }
}
