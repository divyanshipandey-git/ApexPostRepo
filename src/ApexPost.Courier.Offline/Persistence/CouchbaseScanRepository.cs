using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Lite;
using Couchbase.Lite.Query;
using Microsoft.Extensions.Logging;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;

namespace ApexPost.Courier.Offline.Persistence;

/// <summary>
/// Couchbase Lite implementation of IScanRepository.
/// </summary>
public sealed class CouchbaseScanRepository : IScanRepository
{
    private readonly CourierDatabase _database;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<CouchbaseScanRepository>? _logger;

    // SQL++ statements. Every value is bound through a named parameter ($name), never concatenated.
    // "_default" is the Couchbase Lite default collection.
    private const string SelectAllSql =
        "SELECT * FROM _default AS s";

    private const string SelectByTrackingNumberSql =
        "SELECT * FROM _default AS s WHERE s.trackingNumber = $trackingNumber ORDER BY s.scannedAtUtc ASC";

    private const string SelectBySyncStatusSql =
        "SELECT * FROM _default AS s WHERE s.syncStatus = $syncStatus ORDER BY s.scannedAtUtc ASC";

    private const string CountByTypeForCourierSql =
        "SELECT s.scanType AS scanType, COUNT(*) AS total FROM _default AS s " +
        "WHERE s.courierId = $courierId GROUP BY s.scanType";

    public CouchbaseScanRepository(CourierDatabase database, ILogger<CouchbaseScanRepository>? logger = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async Task<ShipmentScan> CreateAsync(ShipmentScan scan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scan);

        var document = new MutableDocument(scan.Id);
        var json = JsonSerializer.Serialize(scan, _jsonOptions);
        document.SetData(JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions)!);

        await Task.Run(() => _database.DefaultCollection.Save(document), cancellationToken);
        return scan;
    }

    public async Task<ShipmentScan?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var document = await Task.Run(() => _database.DefaultCollection.GetDocument(id), cancellationToken);
        if (document == null) return null;

        return DeserializeScan(document);
    }

    public async Task<ShipmentScan> UpdateAsync(ShipmentScan scan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scan);

        var document = await Task.Run(() => _database.DefaultCollection.GetDocument(scan.Id), cancellationToken)
            ?? throw new InvalidOperationException($"Scan with ID {scan.Id} not found");

        var mutableDoc = document.ToMutable();
        var json = JsonSerializer.Serialize(scan, _jsonOptions);
        mutableDoc.SetData(JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions)!);

        await Task.Run(() => _database.DefaultCollection.Save(mutableDoc), cancellationToken);
        return scan;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        var document = await Task.Run(() => _database.DefaultCollection.GetDocument(id), cancellationToken);
        if (document != null)
        {
            await Task.Run(() => _database.DefaultCollection.Delete(document), cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ShipmentScan>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        using var query = _database.Database.CreateQuery(SelectAllSql);
        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<ShipmentScan>> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);

        using var query = _database.Database.CreateQuery(SelectByTrackingNumberSql);
        query.Parameters = new Parameters().SetString("trackingNumber", trackingNumber);

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<ShipmentScan>> GetBySyncStatusAsync(SyncStatus syncStatus, CancellationToken cancellationToken = default)
    {
        using var query = _database.Database.CreateQuery(SelectBySyncStatusSql);
        query.Parameters = new Parameters().SetString("syncStatus", syncStatus.ToString());

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<ScanType, int>> GetScanCountsByTypeForCourierAsync(string courierId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(courierId);

        using var query = _database.Database.CreateQuery(CountByTypeForCourierSql);
        query.Parameters = new Parameters().SetString("courierId", courierId);

        var results = new Dictionary<ScanType, int>();

        await Task.Run(() =>
        {
            using var result = query.Execute();
            foreach (var row in result)
            {
                var scanTypeStr = row.GetString("scanType");
                var count = row.GetInt("total");
                if (Enum.TryParse<ScanType>(scanTypeStr, out var scanType))
                {
                    results[scanType] = count;
                }
                else
                {
                    _logger?.LogWarning("Ignoring scan count row with unknown scanType {ScanType}", scanTypeStr);
                }
            }
        }, cancellationToken);

        return results;
    }

    private async Task<IReadOnlyList<ShipmentScan>> ExecuteQueryAsync(IQuery query, CancellationToken cancellationToken)
    {
        var scans = new List<ShipmentScan>();

        await Task.Run(() =>
        {
            using var result = query.Execute();
            foreach (var row in result)
            {
                var dict = row.GetDictionary(0);
                if (dict != null)
                {
                    var scan = DeserializeFromDictionaryObject(dict);
                    if (scan != null) scans.Add(scan);
                }
            }
        }, cancellationToken);

        return scans;
    }

    private ShipmentScan? DeserializeScan(Document document)
    {
        var dict = document.ToDictionary();
        return DeserializeFromDictionary(dict, document.Id);
    }

    private ShipmentScan? DeserializeFromDictionaryObject(DictionaryObject dict)
    {
        var standardDict = new Dictionary<string, object?>();
        foreach (var key in dict.Keys)
        {
            standardDict[key] = dict.GetValue(key);
        }

        return DeserializeFromDictionary(standardDict, standardDict.TryGetValue("id", out var id) ? id?.ToString() : null);
    }

    /// <summary>
    /// Converts a stored document into a <see cref="ShipmentScan"/>. A document that cannot be read is
    /// logged (with its id) and skipped; it is never swallowed silently.
    /// </summary>
    private ShipmentScan? DeserializeFromDictionary(Dictionary<string, object?> dict, string? documentId)
    {
        try
        {
            var json = JsonSerializer.Serialize(dict, _jsonOptions);
            var scan = JsonSerializer.Deserialize<ShipmentScan>(json, _jsonOptions);
            if (scan is null)
            {
                _logger?.LogError("Scan document {DocumentId} deserialized to null and was skipped", documentId ?? "(unknown)");
            }

            return scan;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            _logger?.LogError(ex, "Scan document {DocumentId} is corrupt and could not be read; it was skipped", documentId ?? "(unknown)");
            return null;
        }
    }
}
