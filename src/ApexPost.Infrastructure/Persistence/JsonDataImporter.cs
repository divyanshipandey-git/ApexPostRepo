using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApexPost.Application.Services.Auditing;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence.SqlEntities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

public sealed record JsonImportPreview(string Fingerprint, IReadOnlyDictionary<string, int> SourceCounts);
public sealed record JsonImportResult(bool AlreadyImported, string Fingerprint, IReadOnlyDictionary<string, int> ImportedCounts);

/// <summary>Imports a read-only snapshot of existing JSON files into an empty business database.</summary>
public sealed class JsonDataImporter(IDbContextFactory<ApexPostDbContext> contextFactory)
{
    private static readonly string[] SourceFiles =
    [
        "branches.json", "counters.json", "staff.json", "staff-credentials.json",
        "products.json", "inventory.json", "counter-transactions.json", "shipments.json", "money-orders.json", "audit.jsonl"
    ];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<JsonImportPreview> PreviewAsync(string dataDirectory, CancellationToken cancellationToken = default)
    {
        var snapshot = await ReadSnapshotAsync(dataDirectory, cancellationToken);
        Validate(snapshot);
        return new JsonImportPreview(snapshot.Fingerprint, snapshot.Counts);
    }

    public async Task<JsonImportResult> ImportAsync(string dataDirectory, CancellationToken cancellationToken = default)
    {
        var snapshot = await ReadSnapshotAsync(dataDirectory, cancellationToken);
        Validate(snapshot);
        var strategyContext = await contextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            var strategy = strategyContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
                var existingRun = await db.ImportRuns.AsNoTracking().SingleOrDefaultAsync(
                    row => row.SourceFingerprint == snapshot.Fingerprint, cancellationToken);
                if (existingRun is not null)
                    return new JsonImportResult(true, snapshot.Fingerprint, snapshot.Counts);

                if (await HasBusinessDataAsync(db, cancellationToken))
                    throw new InvalidOperationException("The target database already contains business data without a matching import record. No import was performed.");

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                db.Branches.AddRange(snapshot.Branches);
                db.Counters.AddRange(snapshot.Counters);
                db.Staff.AddRange(snapshot.Staff);
                db.StaffCredentials.AddRange(snapshot.Credentials);
                db.Products.AddRange(snapshot.Products);
                db.BranchStock.AddRange(snapshot.Stock);
                db.CounterTransactions.AddRange(snapshot.Transactions.Select(SqlEntityMapper.ToRecord));
                db.Shipments.AddRange(snapshot.Shipments);
                db.MoneyOrders.AddRange(snapshot.MoneyOrders.Select(SqlEntityMapper.ToRecord));
                db.AuditEntries.AddRange(snapshot.AuditEvents.Select(SqlEntityMapper.ToRecord));
                db.ImportRuns.Add(new ImportRunRecord
                {
                    SourceFingerprint = snapshot.Fingerprint,
                    ImportedAtUtc = DateTimeOffset.UtcNow,
                    ImporterVersion = "1",
                    SourceCount = snapshot.Counts.Values.Sum(),
                    ImportedCount = snapshot.Counts.Values.Sum(),
                    SkippedCount = 0
                });
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new JsonImportResult(false, snapshot.Fingerprint, snapshot.Counts);
            });
        }
        finally
        {
            await strategyContext.DisposeAsync();
        }
    }

    private static async Task<bool> HasBusinessDataAsync(ApexPostDbContext db, CancellationToken cancellationToken) =>
        await db.Branches.AnyAsync(cancellationToken) || await db.Counters.AnyAsync(cancellationToken) ||
        await db.Staff.AnyAsync(cancellationToken) || await db.Products.AnyAsync(cancellationToken) ||
        await db.BranchStock.AnyAsync(cancellationToken) || await db.CounterTransactions.AnyAsync(cancellationToken) ||
        await db.Shipments.AnyAsync(cancellationToken) || await db.MoneyOrders.AnyAsync(cancellationToken) ||
        await db.StockMovements.AnyAsync(cancellationToken) || await db.AuditEntries.AnyAsync(cancellationToken);

    private static async Task<Snapshot> ReadSnapshotAsync(string dataDirectory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var directory = Path.GetFullPath(dataDirectory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The selected JSON data directory does not exist.");
        using var fingerprintData = new MemoryStream();
        foreach (var file in SourceFiles)
        {
            var path = Path.Combine(directory, file);
            fingerprintData.Write(Encoding.UTF8.GetBytes(file));
            fingerprintData.WriteByte(0);
            if (File.Exists(path))
                await using (var stream = File.OpenRead(path)) await stream.CopyToAsync(fingerprintData, cancellationToken);
            fingerprintData.WriteByte(0xFF);
        }
        var fingerprint = Convert.ToHexString(SHA256.HashData(fingerprintData.ToArray()));
        var branches = await LoadAsync<Branch>(directory, "branches.json", cancellationToken);
        var counters = await LoadAsync<Counter>(directory, "counters.json", cancellationToken);
        var staff = await LoadAsync<Staff>(directory, "staff.json", cancellationToken);
        var credentials = await LoadAsync<StaffCredential>(directory, "staff-credentials.json", cancellationToken);
        var products = await LoadAsync<Product>(directory, "products.json", cancellationToken);
        var stock = await LoadAsync<InventoryStock>(directory, "inventory.json", cancellationToken);
        var transactions = await LoadAsync<CounterTransaction>(directory, "counter-transactions.json", cancellationToken);
        var shipments = await LoadAsync<Shipment>(directory, "shipments.json", cancellationToken);
        var moneyOrders = await LoadAsync<MoneyOrder>(directory, "money-orders.json", cancellationToken);
        var auditEvents = await LoadAuditEventsAsync(directory, cancellationToken);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["branches"] = branches.Length, ["counters"] = counters.Length, ["staff"] = staff.Length,
            ["credentials"] = credentials.Length, ["products"] = products.Length, ["stock"] = stock.Length,
            ["transactions"] = transactions.Length, ["shipments"] = shipments.Length,
            ["moneyOrders"] = moneyOrders.Length, ["auditEvents"] = auditEvents.Length
        };
        return new Snapshot(fingerprint, counts, branches, counters, staff, credentials, products, stock, transactions, shipments, moneyOrders, auditEvents);
    }

    private static async Task<AuditActionRecord[]> LoadAuditEventsAsync(string directory, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, "audit.jsonl");
        if (!File.Exists(path)) return [];
        var records = new List<AuditActionRecord>();
        using var reader = new StreamReader(File.OpenRead(path));
        var lineNumber = 0;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var entry = JsonSerializer.Deserialize<AuditActionRecord>(line, JsonOptions)
                    ?? throw new JsonException();
                ValidateAuditEvent(entry, lineNumber);
                records.Add(entry);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Audit JSONL line {lineNumber} is invalid; no import was performed.", exception);
            }
        }
        return records.ToArray();
    }

    private static void ValidateAuditEvent(AuditActionRecord entry, int lineNumber)
    {
        var allowedRouteKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "controller", "action", "id", "transactionId", "branchCode", "sku", "shipmentId" };
        if (entry.Timestamp.Offset != TimeSpan.Zero ||
            entry.StaffId?.Length > 128 || entry.Role?.Length > 64 || entry.BranchCode?.Length > 10 ||
            string.IsNullOrWhiteSpace(entry.Controller) || entry.Controller.Length > 128 ||
            string.IsNullOrWhiteSpace(entry.Action) || entry.Action.Length > 128 ||
            entry.StatusCode < 0 || entry.DurationMilliseconds < 0 || entry.CorrelationId?.Length > 128 ||
            entry.RouteValues is null || entry.RouteValues.Count > 10 ||
            entry.RouteValues.Any(pair => !allowedRouteKeys.Contains(pair.Key) || pair.Value?.Length > 256 || pair.Value?.Any(char.IsControl) == true))
            throw new InvalidDataException($"Audit JSONL line {lineNumber} contains invalid field lengths or required values; no import was performed.");
    }

    private static async Task<T[]> LoadAsync<T>(string directory, string file, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, file);
        if (!File.Exists(path)) return [];
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T[]>(stream, JsonOptions, cancellationToken) ?? [];
    }

    private static void Validate(Snapshot snapshot)
    {
        if (snapshot.Branches.Select(item => item.Code.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Branches.Length ||
            snapshot.Staff.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Staff.Length ||
            snapshot.Staff.Select(item => item.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Staff.Length ||
            snapshot.Products.Select(item => item.Sku.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Products.Length ||
            snapshot.Transactions.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Transactions.Length)
            throw new InvalidDataException("The JSON snapshot contains duplicate business identifiers and cannot be imported safely.");
        var branchCodes = snapshot.Branches.Select(item => item.Code.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var staffIds = snapshot.Staff.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var productSkus = snapshot.Products.Select(item => item.Sku.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var counterIds = snapshot.Counters.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var transactionIds = snapshot.Transactions.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var shipmentIds = snapshot.Shipments.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (snapshot.Counters.Any(item => !branchCodes.Contains(item.BranchCode.Value)) ||
            snapshot.Staff.Any(item => !branchCodes.Contains(item.HomeBranchCode.Value)) ||
            snapshot.Credentials.Any(item => !staffIds.Contains(item.StaffId)) ||
            snapshot.Stock.Any(item => !branchCodes.Contains(item.BranchCode.Value) || !productSkus.Contains(item.Sku.Value)) ||
            snapshot.Transactions.Any(item => !branchCodes.Contains(item.BranchCode.Value) || !counterIds.Contains(item.CounterId) || !staffIds.Contains(item.ClerkId)) ||
            snapshot.Shipments.Any(item => item.BranchCode is not null && !branchCodes.Contains(item.BranchCode.Value)) ||
            snapshot.Shipments.Any(item => item.CounterTransactionId is not null && !transactionIds.Contains(item.CounterTransactionId)) ||
            snapshot.MoneyOrders.Any(item => item.BranchCode is not null && !branchCodes.Contains(item.BranchCode)) ||
            snapshot.MoneyOrders.Any(item => item.TransactionId is not null && !transactionIds.Contains(item.TransactionId)) ||
            snapshot.Transactions.SelectMany(item => item.LineItems).Any(line =>
                line.ReferenceId?.StartsWith("SHP-", StringComparison.OrdinalIgnoreCase) == true &&
                !shipmentIds.Contains(line.ReferenceId)))
            throw new InvalidDataException("The JSON snapshot contains a broken branch, staff, counter, product, or transaction reference.");
        if (snapshot.Credentials.Any(item => string.IsNullOrWhiteSpace(item.PasswordHash) || item.PasswordHash.Length > 512))
            throw new InvalidDataException("The JSON snapshot contains an invalid password hash.");
    }

    private sealed record Snapshot(
        string Fingerprint, IReadOnlyDictionary<string, int> Counts, Branch[] Branches, Counter[] Counters,
        Staff[] Staff, StaffCredential[] Credentials, Product[] Products, InventoryStock[] Stock,
        CounterTransaction[] Transactions, Shipment[] Shipments, MoneyOrder[] MoneyOrders, AuditActionRecord[] AuditEvents);
}
