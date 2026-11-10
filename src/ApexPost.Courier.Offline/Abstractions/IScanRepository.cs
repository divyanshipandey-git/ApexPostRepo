using ApexPost.Courier.Offline.Domain;

namespace ApexPost.Courier.Offline.Abstractions;

/// <summary>
/// Repository interface for managing shipment scan documents.
/// </summary>
public interface IScanRepository
{
    /// <summary>
    /// Creates a new scan document.
    /// </summary>
    Task<ShipmentScan> CreateAsync(ShipmentScan scan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a scan document by its ID (key-value lookup).
    /// </summary>
    Task<ShipmentScan?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing scan document.
    /// </summary>
    Task<ShipmentScan> UpdateAsync(ShipmentScan scan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a scan document by ID.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all scan documents.
    /// </summary>
    Task<IReadOnlyList<ShipmentScan>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all scans for a specific tracking number, ordered by scannedAtUtc.
    /// </summary>
    Task<IReadOnlyList<ShipmentScan>> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all scans with a specific sync status.
    /// </summary>
    Task<IReadOnlyList<ShipmentScan>> GetBySyncStatusAsync(SyncStatus syncStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of scans grouped by scan type for a specific courier.
    /// </summary>
    Task<IReadOnlyDictionary<ScanType, int>> GetScanCountsByTypeForCourierAsync(string courierId, CancellationToken cancellationToken = default);
}