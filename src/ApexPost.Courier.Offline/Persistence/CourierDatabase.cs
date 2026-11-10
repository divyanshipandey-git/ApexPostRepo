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
    private readonly Collection _defaultCollection;
    private readonly List<ListenerToken> _changeListenerTokens = [];
    private readonly object _listenerLock = new();
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
        _defaultCollection = _database.GetDefaultCollection();
        _logger?.LogInformation("Courier database initialized at {Path}", config.Directory);

        // Create value indexes for query performance
        CreateIndexes();
    }

    /// <summary>
    /// Gets the underlying Couchbase Lite database instance.
    /// </summary>
    public Database Database => _database;

    /// <summary>
    /// Gets the Couchbase Lite default collection (<c>_default._default</c>), which stores the scan documents.
    /// </summary>
    public Collection DefaultCollection => _defaultCollection;

    /// <summary>
    /// Registers a change listener on the default collection. Any number of listeners may be registered;
    /// each one gets its own token.
    /// </summary>
    /// <param name="handler">Handler for document changes.</param>
    /// <returns>A token that can be passed to <see cref="RemoveChangeListener(ListenerToken)"/>.</returns>
    public ListenerToken AddChangeListener(Action<IEnumerable<string>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var token = _defaultCollection.AddChangeListener((sender, args) =>
        {
            var changedIds = args.DocumentIDs;
            if (changedIds.Count > 0)
            {
                handler(changedIds);
            }
        });

        lock (_listenerLock)
        {
            _changeListenerTokens.Add(token);
        }

        return token;
    }

    /// <summary>
    /// Removes one previously registered change listener.
    /// </summary>
    /// <param name="token">The token returned by <see cref="AddChangeListener"/>.</param>
    public void RemoveChangeListener(ListenerToken token)
    {
        lock (_listenerLock)
        {
            var index = _changeListenerTokens.IndexOf(token);
            if (index < 0) return;

            _changeListenerTokens.RemoveAt(index);
        }

        _defaultCollection.RemoveChangeListener(token);
        _logger?.LogDebug("Change listener removed");
    }

    /// <summary>
    /// Removes every registered change listener.
    /// </summary>
    public void RemoveChangeListener()
    {
        ListenerToken[] tokens;
        lock (_listenerLock)
        {
            tokens = [.. _changeListenerTokens];
            _changeListenerTokens.Clear();
        }

        foreach (var token in tokens)
        {
            _defaultCollection.RemoveChangeListener(token);
        }

        if (tokens.Length > 0)
        {
            _logger?.LogDebug("Removed {Count} change listener(s)", tokens.Length);
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

            // Index on courierId for the per-courier scan count query
            DefaultCollection.CreateIndex("idx_courierId", IndexBuilder.ValueIndex(ValueIndexItem.Property("courierId")));

            _logger?.LogDebug("Value indexes created for trackingNumber, syncStatus and courierId");
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