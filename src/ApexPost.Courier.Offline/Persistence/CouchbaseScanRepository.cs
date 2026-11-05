using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Lite;
using Couchbase.Lite.Query;
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

    public CouchbaseScanRepository(CourierDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
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
        var query = QueryBuilder.Select(SelectResult.All())
            .From(DataSource.Collection(_database.DefaultCollection));

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<ShipmentScan>> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);

        var query = QueryBuilder.Select(SelectResult.All())
            .From(DataSource.Collection(_database.DefaultCollection))
            .Where(Expression.Property("trackingNumber").EqualTo(Expression.String(trackingNumber)))
            .OrderBy(Ordering.Property("scannedAtUtc").Ascending());

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<ShipmentScan>> GetBySyncStatusAsync(SyncStatus syncStatus, CancellationToken cancellationToken = default)
    {
        var query = QueryBuilder.Select(SelectResult.All())
            .From(DataSource.Collection(_database.DefaultCollection))
            .Where(Expression.Property("syncStatus").EqualTo(Expression.String(syncStatus.ToString())));

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<ScanType, int>> GetScanCountsByTypeForCourierAsync(string courierId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(courierId);

        var query = QueryBuilder.Select(
                SelectResult.Expression(Expression.Property("scanType")),
                SelectResult.Expression(Function.Count(Expression.All())))
            .From(DataSource.Collection(_database.DefaultCollection))
            .Where(Expression.Property("courierId").EqualTo(Expression.String(courierId)))
            .GroupBy(Expression.Property("scanType"));

        var results = new Dictionary<ScanType, int>();

        await Task.Run(() =>
        {
            using var result = query.Execute();
            foreach (var row in result)
            {
                var scanTypeStr = row.GetString(0);
                var count = row.GetInt(1);
                if (Enum.TryParse<ScanType>(scanTypeStr, out var scanType))
                {
                    results[scanType] = count;
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
        return DeserializeFromDictionary(dict);
    }

    private ShipmentScan? DeserializeFromDictionaryObject(DictionaryObject dict)
    {
        try
        {
            var standardDict = new Dictionary<string, object?>();
            foreach (var key in dict.Keys)
            {
                standardDict[key] = dict.GetValue(key);
            }
            var json = JsonSerializer.Serialize(standardDict, _jsonOptions);
            return JsonSerializer.Deserialize<ShipmentScan>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private ShipmentScan? DeserializeFromDictionary(Dictionary<string, object?> dict)
    {
        try
        {
            var json = JsonSerializer.Serialize(dict, _jsonOptions);
            return JsonSerializer.Deserialize<ShipmentScan>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }
}