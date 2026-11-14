using ApexPost.Courier.Domain;
using ApexPost.Courier.Offline.Services;

namespace ApexPost.Specs.Support;

/// <summary>In-memory stand-in for the Couchbase Lite repository (the real one is covered by the Courier.Offline tests).</summary>
public sealed class InMemoryScanRepository : IScanRepository
{
    private readonly Dictionary<string, ShipmentScan> _scans = [];

    public IReadOnlyCollection<ShipmentScan> All => _scans.Values;

    public Task<ShipmentScan> CreateAsync(ShipmentScan scan, CancellationToken cancellationToken = default)
    {
        _scans[scan.Id] = scan;
        return Task.FromResult(scan);
    }

    public Task<ShipmentScan?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_scans.GetValueOrDefault(id));

    public Task<ShipmentScan> UpdateAsync(ShipmentScan scan, CancellationToken cancellationToken = default)
    {
        _scans[scan.Id] = scan;
        return Task.FromResult(scan);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _scans.Remove(id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ShipmentScan>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ShipmentScan>>(_scans.Values.ToList());

    public Task<IReadOnlyList<ShipmentScan>> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ShipmentScan>>(_scans.Values.Where(s => s.TrackingNumber == trackingNumber).ToList());

    public Task<IReadOnlyList<ShipmentScan>> GetBySyncStatusAsync(SyncStatus syncStatus, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ShipmentScan>>(_scans.Values.Where(s => s.SyncStatus == syncStatus).OrderBy(s => s.ScannedAtUtc).ToList());

    public Task<IReadOnlyDictionary<ScanType, int>> GetScanCountsByTypeForCourierAsync(string courierId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<ScanType, int>>(
            _scans.Values.Where(s => s.CourierId == courierId).GroupBy(s => s.ScanType).ToDictionary(g => g.Key, g => g.Count()));
}

/// <summary>An uploader whose answers the scenario scripts: accept everything, accept only N scans, or reject everything.</summary>
public sealed class ScriptedUploader : IShipmentUploader
{
    private int _accepted;

    public bool AcceptAll { get; set; }

    /// <summary>When AcceptAll is false: how many uploads (per runner life) are accepted before the rest fail.</summary>
    public int AcceptFirst { get; set; }

    public Task<bool> UploadAsync(ShipmentScan scan, CancellationToken cancellationToken = default)
    {
        if (AcceptAll)
        {
            return Task.FromResult(true);
        }

        if (_accepted < AcceptFirst)
        {
            _accepted++;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
