using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApexPost.Api.DTOs;
using ApexPost.Courier.Console.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Courier.Console.Services;

/// <summary>
/// Sync service that replaces the fake IShipmentUploader from Part A with real API calls.
/// </summary>
public interface ISyncService
{
    event EventHandler<SyncStatusChangedEventArgs>? SyncStatusChanged;
    event EventHandler<int>? PendingScansCountChanged;

    SyncState CurrentStatus { get; }
    DateTimeOffset? LastSyncTime { get; }
    int PendingScansCount { get; }

    Task<SyncResult> SyncPendingScansAsync(CancellationToken cancellationToken = default);
    Task OnConnectivityChangedAsync(bool isConnected, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sync state for the console app (different from Models.SyncStatus to avoid ambiguity).
/// </summary>
public enum SyncState
{
    Idle,
    Syncing,
    Offline,
    CircuitOpen,
    Error
}

public sealed class SyncResult
{
    public int ScansSynced { get; init; }
    public int ScansFailed { get; init; }
    public int ScansSkipped { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsSuccess => ScansFailed == 0 && string.IsNullOrEmpty(ErrorMessage);
}

public sealed class SyncStatusChangedEventArgs : EventArgs
{
    public SyncState PreviousStatus { get; init; }
    public SyncState CurrentStatus { get; init; }
}

public sealed class SyncService : ISyncService, IDisposable
{
    private readonly IApexApiClient _apiClient;
    private readonly ICourierDatabase _database;
    private readonly ILogger<SyncService> _logger;
    private readonly SyncOptions _options;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly CancellationTokenSource _autoSyncCts = new();
    
    private SyncState _currentStatus = SyncState.Idle;
    private DateTimeOffset? _lastSyncTime;
    private int _pendingScansCount = 0;
    private bool _isConnected = true;
    private bool _circuitOpen = false;
    private DateTimeOffset? _circuitOpenedAt;
    private int _consecutiveFailures = 0;

    public event EventHandler<SyncStatusChangedEventArgs>? SyncStatusChanged;
    public event EventHandler<int>? PendingScansCountChanged;

    public SyncState CurrentStatus => _currentStatus;
    public DateTimeOffset? LastSyncTime => _lastSyncTime;
    public int PendingScansCount => _pendingScansCount;

    public SyncService(
        IApexApiClient apiClient,
        ICourierDatabase database,
        IOptions<SyncOptions> options,
        ILogger<SyncService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SyncResult> SyncPendingScansAsync(CancellationToken cancellationToken = default)
    {
        if (!_syncLock.Wait(0))
        {
            _logger.LogWarning("Sync already in progress, skipping");
            return new SyncResult { ErrorMessage = "Sync already in progress" };
        }

        try
        {
            var previousStatus = _currentStatus;
            SetStatus(SyncState.Syncing);

            if (!_isConnected)
            {
                SetStatus(SyncState.Offline);
                return new SyncResult { ErrorMessage = "Device is offline" };
            }

            if (_circuitOpen)
            {
                SetStatus(SyncState.CircuitOpen);
                return new SyncResult { ErrorMessage = "Circuit breaker is open - server unavailable" };
            }

            _logger.LogInformation("Starting sync of pending scans");

            var pendingScans = await _database.GetPendingScansAsync();
            if (pendingScans.Count == 0)
            {
                _logger.LogInformation("No pending scans to sync");
                SetStatus(SyncState.Idle);
                return new SyncResult { ErrorMessage = string.Empty };
            }

            _logger.LogInformation("Found {Count} pending scans to sync", pendingScans.Count);

            // Batch upload (max 20 per request as per requirements)
            var batches = pendingScans
                .Select((scan, index) => new { scan, index })
                .GroupBy(x => x.index / _options.BatchSize)
                .Select(g => g.Select(x => x.scan).ToList())
                .ToList();

            int scansSynced = 0;
            int scansSkipped = 0;
            int scansFailed = 0;
            string? errorMessage = null;
            int totalProcessed = 0;

            foreach (var batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_isConnected || _circuitOpen)
                {
                    _logger.LogWarning("Sync interrupted: connected={Connected}, circuitOpen={CircuitOpen}", _isConnected, _circuitOpen);
                    break;
                }

                var uploadRequest = new ScanUploadRequest
                {
                    Scans = batch.Select(s => s.ToApiRequest()).ToList()
                };

                try
                {
                    var response = await _apiClient.UploadScansAsync(batch.First().TrackingNumber, uploadRequest, cancellationToken);
                    
                    if (response != null)
                    {
                        scansSynced += response.Accepted;
                        scansSkipped += response.DuplicatesSkipped;
                        scansFailed += response.Rejected;

                        // Update local database with sync results
                        foreach (var scan in batch)
                        {
                            var rejectionDetail = response.RejectionDetails.FirstOrDefault(r => r.ScanId == scan.ScanId);
                            if (rejectionDetail != null)
                            {
                                await _database.MarkScanFailedAsync(scan.ScanId, rejectionDetail.Reason);
                            }
                            else
                            {
                                await _database.MarkScanSyncedAsync(scan.ScanId);
                            }
                        }
                    }

                    _consecutiveFailures = 0;
                }
                catch (ApiException ex) when (ex.StatusCode == 401)
                {
                    _logger.LogWarning("Authentication failed during sync (401), clearing token");
                    SetStatus(SyncState.Error);
                    errorMessage = "Authentication failed - please log in again";
                    scansFailed += batch.Count;
                    _consecutiveFailures++;
                    break;
                }
                catch (ApiException ex) when (ex.StatusCode >= 500 || ex.StatusCode == 408)
                {
                    _logger.LogWarning("Server error during sync (status {StatusCode}), will retry later", ex.StatusCode);
                    scansFailed += batch.Count;
                    _consecutiveFailures++;
                    
                    if (_consecutiveFailures >= _options.CircuitBreakerFailureThreshold)
                    {
                        OpenCircuit();
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Network error during sync");
                    scansFailed += batch.Count;
                    errorMessage = "Network error - working offline";
                    _consecutiveFailures++;
                    
                    if (_consecutiveFailures >= _options.CircuitBreakerFailureThreshold)
                    {
                        OpenCircuit();
                    }
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Sync cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during sync");
                    scansFailed += batch.Count;
                    errorMessage = ex.Message;
                    _consecutiveFailures++;
                }

                totalProcessed += batch.Count;
                UpdatePendingCount(pendingScans.Count - totalProcessed);
            }

            var result = new SyncResult
            {
                ScansSynced = scansSynced,
                ScansSkipped = scansSkipped,
                ScansFailed = scansFailed,
                ErrorMessage = errorMessage
            };

            if (result.IsSuccess)
            {
                _lastSyncTime = DateTimeOffset.UtcNow;
                _logger.LogInformation("Sync completed successfully: {Synced} synced, {Skipped} skipped, {Failed} failed", 
                    scansSynced, scansSkipped, scansFailed);
            }
            else
            {
                _logger.LogWarning("Sync completed with errors: {Synced} synced, {Skipped} skipped, {Failed} failed, Error: {Error}", 
                    scansSynced, scansSkipped, scansFailed, errorMessage);
            }

            SetStatus(SyncState.Idle);
            return result;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task OnConnectivityChangedAsync(bool isConnected, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connectivity changed: {IsConnected}", isConnected);
        _isConnected = isConnected;

        if (isConnected && !_circuitOpen)
        {
            _logger.LogInformation("Connectivity restored, triggering sync");
            SetStatus(SyncState.Idle);
            await SyncPendingScansAsync(cancellationToken);
        }
        else if (!isConnected)
        {
            SetStatus(SyncState.Offline);
        }
    }

    private void SetStatus(SyncState newStatus)
    {
        if (_currentStatus != newStatus)
        {
            var previous = _currentStatus;
            _currentStatus = newStatus;
            
            _logger.LogDebug("Sync status changed: {Previous} -> {Current}", previous, newStatus);
            
            try
            {
                SyncStatusChanged?.Invoke(this, new SyncStatusChangedEventArgs 
                { 
                    PreviousStatus = previous, 
                    CurrentStatus = newStatus 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing SyncStatusChanged event");
            }
        }
    }

    private void UpdatePendingCount(int count)
    {
        _pendingScansCount = count;
        try
        {
            PendingScansCountChanged?.Invoke(this, count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error firing PendingScansCountChanged event");
        }
    }

    private void OpenCircuit()
    {
        if (!_circuitOpen)
        {
            _circuitOpen = true;
            _circuitOpenedAt = DateTimeOffset.UtcNow;
            SetStatus(SyncState.CircuitOpen);
            _logger.LogWarning("Circuit breaker opened - server unavailable after {Threshold} consecutive failures", _options.CircuitBreakerFailureThreshold);
        }
    }

    public void Dispose()
    {
        _syncLock.Dispose();
        _autoSyncCts.Dispose();
    }
}

public sealed class SyncOptions
{
    public int BatchSize { get; set; } = 20;
    public int CircuitBreakerFailureThreshold { get; set; } = 3;
    public TimeSpan CircuitBreakerResetTimeout { get; set; } = TimeSpan.FromSeconds(30);
}