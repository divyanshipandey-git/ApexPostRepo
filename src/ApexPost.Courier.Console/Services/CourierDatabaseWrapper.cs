using System.Collections.Generic;
using System.Threading.Tasks;
using ApexPost.Courier.Console.Models;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Persistence;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Console.Services;

/// <summary>
/// Interface for the courier offline database (wraps Part A Couchbase Lite).
/// </summary>
public interface ICourierDatabase : IDisposable
{
    Task AddScanAsync(LocalScanDocument scan);
    Task<LocalScanDocument?> GetScanByIdAsync(string scanId);
    Task UpdateScanAsync(LocalScanDocument scan);
    Task DeleteScanAsync(string scanId);
    Task<IReadOnlyList<LocalScanDocument>> ListAllScansAsync();
    Task<IReadOnlyList<LocalScanDocument>> GetScansByTrackingNumberAsync(string trackingNumber);
    Task<IReadOnlyList<LocalScanDocument>> GetPendingScansAsync();
    Task<Dictionary<string, int>> GetScanCountByTypeAsync(string courierId);
    Task MarkScanSyncedAsync(string scanId);
    Task MarkScanFailedAsync(string scanId, string reason);
    event EventHandler<DocumentChangedEventArgs>? DocumentChanged;
}

public sealed class DocumentChangedEventArgs : EventArgs
{
    public string DocumentId { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public bool IsPending { get; init; }
}

/// <summary>
/// Wrapper that adapts the Part A Couchbase Lite database to the console app interface.
/// </summary>
public sealed class CourierDatabaseWrapper : ICourierDatabase
{
    private readonly CourierDatabase _database;
    private readonly CouchbaseScanRepository _repository;
    private readonly ILogger<CourierDatabaseWrapper> _logger;

    public event EventHandler<DocumentChangedEventArgs>? DocumentChanged;

    public CourierDatabaseWrapper(
        CourierDatabase database,
        CouchbaseScanRepository repository,
        ILogger<CourierDatabaseWrapper> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _database.AddChangeListener(OnDocumentsChanged);
    }

    private void OnDocumentsChanged(System.Collections.Generic.IEnumerable<string> documentIds)
    {
        foreach (var id in documentIds)
        {
            var args = new DocumentChangedEventArgs
            {
                DocumentId = id,
                TrackingNumber = string.Empty,
                IsPending = false
            };
            
            try
            {
                DocumentChanged?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing DocumentChanged event for {DocumentId}", id);
            }
        }
    }

    public async Task AddScanAsync(LocalScanDocument scan)
    {
        var shipmentScan = scan.ToOffline();
        await _repository.CreateAsync(shipmentScan);
    }

    public async Task<LocalScanDocument?> GetScanByIdAsync(string scanId)
    {
        var scan = await _repository.GetByIdAsync(scanId);
        return scan != null ? LocalScanDocument.FromOffline(scan) : null;
    }

    public async Task UpdateScanAsync(LocalScanDocument scan)
    {
        var shipmentScan = scan.ToOffline();
        await _repository.UpdateAsync(shipmentScan);
    }

    public async Task DeleteScanAsync(string scanId)
    {
        await _repository.DeleteAsync(scanId);
    }

    public async Task<IReadOnlyList<LocalScanDocument>> ListAllScansAsync()
    {
        var scans = await _repository.ListAllAsync();
        return scans.Select(LocalScanDocument.FromOffline).ToList();
    }

    public async Task<IReadOnlyList<LocalScanDocument>> GetScansByTrackingNumberAsync(string trackingNumber)
    {
        var scans = await _repository.GetByTrackingNumberAsync(trackingNumber);
        return scans.Select(LocalScanDocument.FromOffline).ToList();
    }

    public async Task<IReadOnlyList<LocalScanDocument>> GetPendingScansAsync()
    {
        var scans = await _repository.GetBySyncStatusAsync(ApexPost.Courier.Offline.Domain.SyncStatus.Pending);
        return scans.Select(LocalScanDocument.FromOffline).ToList();
    }

    public async Task<Dictionary<string, int>> GetScanCountByTypeAsync(string courierId)
    {
        var counts = await _repository.GetScanCountsByTypeForCourierAsync(courierId);
        return counts.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
    }

    public async Task MarkScanSyncedAsync(string scanId)
    {
        var scan = await _repository.GetByIdAsync(scanId);
        if (scan != null)
        {
            var updatedScan = new ApexPost.Courier.Offline.Domain.ShipmentScan
            {
                Id = scan.Id,
                TrackingNumber = scan.TrackingNumber,
                ScanType = scan.ScanType,
                CourierId = scan.CourierId,
                Location = scan.Location,
                ScannedAtUtc = scan.ScannedAtUtc,
                Notes = scan.Notes,
                SyncStatus = ApexPost.Courier.Offline.Domain.SyncStatus.Synced
            };
            await _repository.UpdateAsync(updatedScan);
        }
    }

    public async Task MarkScanFailedAsync(string scanId, string reason)
    {
        var scan = await _repository.GetByIdAsync(scanId);
        if (scan != null)
        {
            var updatedScan = new ApexPost.Courier.Offline.Domain.ShipmentScan
            {
                Id = scan.Id,
                TrackingNumber = scan.TrackingNumber,
                ScanType = scan.ScanType,
                CourierId = scan.CourierId,
                Location = scan.Location,
                ScannedAtUtc = scan.ScannedAtUtc,
                Notes = string.IsNullOrEmpty(scan.Notes) ? reason : $"{scan.Notes}; Sync failed: {reason}",
                SyncStatus = ApexPost.Courier.Offline.Domain.SyncStatus.Failed
            };
            await _repository.UpdateAsync(updatedScan);
        }
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}