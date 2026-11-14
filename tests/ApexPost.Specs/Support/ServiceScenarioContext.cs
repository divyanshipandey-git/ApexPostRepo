using ApexPost.Api.DTOs;
using ApexPost.Api.Services;
using ApexPost.Courier.Domain;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.IntegrationTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Specs.Support;

/// <summary>A clock that only moves when the scenario says so.</summary>
public sealed class ScenarioClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2025, 3, 10, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// State of ONE service-level scenario: a private SQLite database, a frozen clock, the real upload service,
/// and an in-memory device outbox. Created per scenario by Reqnroll context injection.
/// </summary>
public sealed class ServiceScenarioContext : IDisposable
{
    public SqliteTestDatabase Database { get; } = new();

    public ScenarioClock Clock { get; } = new();

    public string CourierId { get; set; } = "COURIER-001";

    public List<ScanDocumentRequest> RecordedScans { get; } = [];

    public ScanUploadResponse? LastUpload { get; set; }

    public Exception? LastError { get; set; }

    // Device side (outbox scenarios)
    public InMemoryScanRepository DeviceScans { get; } = new();

    public ScriptedUploader Uploader { get; } = new();

    public ScanUploadService CreateUploadService(ApexPostDbContext context) => new(
        new SqlTrackedShipmentRepository(context),
        new SqlUnitOfWork(context),
        NullLogger<ScanUploadService>.Instance,
        Clock);

    public void Dispose() => Database.Dispose();
}
