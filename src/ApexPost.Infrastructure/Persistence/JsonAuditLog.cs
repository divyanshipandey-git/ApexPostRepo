
using System.Text.Json;
using System.Text.Json.Serialization;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Services.Auditing;

namespace ApexPost.Infrastructure.Persistence;

public sealed class JsonAuditLog : IAuditStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonAuditLog(JsonPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _filePath = options.GetFilePath("audit.jsonl");
    }

    public async Task WriteAsync(
        AuditLogEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        ValidateEntry(entry);
        await AppendAsync(entry, cancellationToken);
    }

    public async Task WriteAsync(
        AuditActionRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ValidateRecord(record);
        await AppendAsync(record, cancellationToken);
    }

    private async Task AppendAsync<T>(T entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(cancellationToken);

        try
        {
            var directory = Path.GetDirectoryName(_filePath)
                ?? throw new InvalidOperationException(
                    "Invalid audit log path.");

            Directory.CreateDirectory(directory);

            await using var stream = new FileStream(
                _filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            await JsonSerializer.SerializeAsync(
                stream,
                entry,
                JsonOptions,
                cancellationToken);

            await stream.WriteAsync(
                "\n"u8.ToArray(),
                cancellationToken);

            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void ValidateRecord(AuditActionRecord record)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Controller);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Action);
        ArgumentOutOfRangeException.ThrowIfNegative(record.StatusCode);
        ArgumentOutOfRangeException.ThrowIfNegative(record.DurationMilliseconds);

        if ((record.StaffId?.Length ?? 0) > 128 ||
            (record.Role?.Length ?? 0) > 64 ||
            (record.BranchCode?.Length ?? 0) > 10 ||
            record.Controller.Length > 128 ||
            record.Action.Length > 128 ||
            (record.CorrelationId?.Length ?? 0) > 128 ||
            record.RouteValues.Any(pair => pair.Key.Length > 128 || (pair.Value?.Length ?? 0) > 256))
        {
            throw new ArgumentException("An audit field exceeds its permitted length.", nameof(record));
        }
    }

    private static void ValidateEntry(AuditLogEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.ActorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.EntityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.EntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Outcome);

        if (entry.ActorId.Length > 128 ||
            entry.Action.Length > 128 ||
            entry.EntityType.Length > 128 ||
            entry.EntityId.Length > 256 ||
            entry.Outcome.Length > 64 ||
            (entry.CorrelationId?.Length ?? 0) > 128)
        {
            throw new ArgumentException(
                "An audit field exceeds its permitted length.",
                nameof(entry));
        }
    }
}
