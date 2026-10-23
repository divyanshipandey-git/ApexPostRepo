
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexPost.Infrastructure.Persistence;

public sealed class JsonFileStore<TEntity>
    where TEntity : class
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly Func<TEntity, string> _keySelector;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonFileStore(
        string filePath,
        Func<TEntity, string> keySelector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(keySelector);

        _filePath = Path.GetFullPath(filePath);
        _keySelector = keySelector;
    }

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadAllCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TEntity?> GetByKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var items = await GetAllAsync(cancellationToken);

        return items.FirstOrDefault(item =>
            string.Equals(
                _keySelector(item),
                key.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    public async Task AddAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = (await ReadAllCoreAsync(cancellationToken)).ToList();
            var key = _keySelector(entity);

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    "The entity key cannot be empty.");
            }

            if (items.Any(item => string.Equals(
                    _keySelector(item), key, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"An entity with key '{key}' already exists.");
            }

            items.Add(entity);
            await WriteAllCoreAsync(items, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = (await ReadAllCoreAsync(cancellationToken)).ToList();
            var key = _keySelector(entity);

            var index = items.FindIndex(item =>
                string.Equals(
                    _keySelector(item),
                    key,
                    StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                throw new KeyNotFoundException(
                    $"No entity with key '{key}' exists.");
            }

            items[index] = entity;
            await WriteAllCoreAsync(items, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<TEntity>> ReadAllCoreAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<TEntity>();
        }

        await using var stream = new FileStream(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var entities = await JsonSerializer.DeserializeAsync<List<TEntity>>(
            stream,
            JsonOptions,
            cancellationToken);

        return entities ?? [];
    }

    private async Task WriteAllCoreAsync(
        List<TEntity> entities,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Invalid JSON file path.");

        Directory.CreateDirectory(directory);

        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    entities,
                    JsonOptions,
                    cancellationToken);

                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
