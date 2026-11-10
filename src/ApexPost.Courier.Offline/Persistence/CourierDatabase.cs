using System.Diagnostics;
using Couchbase.Lite;
using Couchbase.Lite.Query;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Offline.Persistence;

/// <summary>
/// Wraps a Couchbase Lite database instance for the courier offline functionality.
/// Implements IDisposable for proper resource cleanup.
/// </summary>
public sealed class CourierDatabase : IDisposable
{
    private readonly Database _database;
    private readonly ILogger<CourierDatabase>? _logger;
    private ListenerToken? _changeListenerToken;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the CourierDatabase class.
    /// </summary>
    /// <param name="databasePath">Optional path for the database. If null, uses default path.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public CourierDatabase(string? databasePath = null, ILogger<CourierDatabase>? logger = null)
    {
        _logger = logger;

        var config = new DatabaseConfiguration
        {
            Directory = databasePath ?? GetDefaultDatabasePath()
        };

        _database = new Database("apexpost_courier", config);
        _logger?.LogInformation("Courier database initialized at {Path}", config.Directory);

        // Create value indexes for query performance
        CreateIndexes();
    }

    /// <summary>
    /// Gets the underlying Couchbase Lite database instance.
    /// </summary>
    public Database Database => _database;

    /// <summary>
    /// Gets the default collection for storing scan documents.
    /// </summary>
    public Collection DefaultCollection => _database.GetCollection("scans") ?? _database.CreateCollection("scans");

    /// <summary>
    /// Registers a change listener on the default collection.
    /// </summary>
    /// <param name="handler">Handler for document changes.</param>
    /// <returns>A token that can be used to remove the listener.</returns>
    public ListenerToken AddChangeListener(Action<IEnumerable<string>> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _changeListenerToken = DefaultCollection.AddChangeListener((sender, args) =>
        {
            var changedIds = args.DocumentIDs;
            if (changedIds.Count > 0)
            {
                handler(changedIds);
            }
        });

        return _changeListenerToken.Value;
    }

    /// <summary>
    /// Removes the registered change listener.
    /// </summary>
    public void RemoveChangeListener()
    {
        if (_changeListenerToken.HasValue)
        {
            DefaultCollection.RemoveChangeListener(_changeListenerToken.Value);
            _changeListenerToken = null;
            _logger?.LogDebug("Change listener removed");
        }
    }

    /// <summary>
    /// Creates value indexes for query performance.
    /// </summary>
    private void CreateIndexes()
    {
        try
        {
            // Index on trackingNumber for GetByTrackingNumberAsync queries
            DefaultCollection.CreateIndex("idx_trackingNumber", IndexBuilder.ValueIndex(ValueIndexItem.Property("trackingNumber")));

            // Index on syncStatus for GetBySyncStatusAsync queries
            DefaultCollection.CreateIndex("idx_syncStatus", IndexBuilder.ValueIndex(ValueIndexItem.Property("syncStatus")));

            _logger?.LogDebug("Value indexes created for trackingNumber and syncStatus");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to create indexes (may already exist)");
        }
    }

    /// <summary>
    /// Gets the default database path.
    /// </summary>
    private static string GetDefaultDatabasePath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, "ApexPost", "CourierOffline");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        RemoveChangeListener();

        try
        {
            _database.Close();
            _logger?.LogInformation("Courier database closed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error closing courier database");
        }

        _disposed = true;
    }
}