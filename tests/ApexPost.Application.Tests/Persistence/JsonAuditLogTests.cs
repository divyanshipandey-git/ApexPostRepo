
using ApexPost.Infrastructure.Persistence;
using System.Text.Json;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

public sealed class JsonAuditLogTests
{
    [Fact]
    public async Task WriteAsync_AppendsAuditRecordAsJsonLine()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "ApexPostTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var options = new JsonPersistenceOptions
            {
                DataDirectory = directory
            };

            var auditLog = new JsonAuditLog(options);

            var entry = new AuditLogEntry(
                DateTimeOffset.UtcNow,
                "staff-001",
                "ProductUpdated",
                "Product",
                "ENV001",
                "Success",
                "correlation-001");

            await auditLog.WriteAsync(entry);

            var path = options.GetFilePath("audit.jsonl");
            var lines = await File.ReadAllLinesAsync(path);

            Assert.Single(lines);

            var saved = JsonSerializer.Deserialize<AuditLogEntry>(
                lines[0],
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            Assert.NotNull(saved);
            Assert.Equal(entry.ActorId, saved.ActorId);
            Assert.Equal(entry.Action, saved.Action);
            Assert.Equal(entry.EntityId, saved.EntityId);
            Assert.Equal(entry.CorrelationId, saved.CorrelationId);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteAsync_AppendsRatherThanOverwriting()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "ApexPostTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var auditLog = new JsonAuditLog(
                new JsonPersistenceOptions
                {
                    DataDirectory = directory
                });

            await auditLog.WriteAsync(new AuditLogEntry(
                DateTimeOffset.UtcNow,
                "staff-001",
                "Created",
                "Product",
                "ENV001",
                "Success"));

            await auditLog.WriteAsync(new AuditLogEntry(
                DateTimeOffset.UtcNow,
                "staff-002",
                "Updated",
                "Product",
                "ENV001",
                "Success"));

            var lines = await File.ReadAllLinesAsync(
                Path.Combine(directory, "audit.jsonl"));

            Assert.Equal(2, lines.Length);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
