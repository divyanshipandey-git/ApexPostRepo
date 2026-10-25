
namespace ApexPost.Application.Abstractions.Persistence;

/// <summary>
/// Defines asynchronous persistence operations for an entity.
/// Implementations are provided by the Infrastructure layer.
/// </summary>
public interface IRepository<TEntity, in TKey>
    where TEntity : class
    where TKey : notnull
{
    Task<TEntity?> GetByIdAsync(
        TKey id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(
        TEntity entity,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        TEntity entity,
        CancellationToken cancellationToken = default);
}
