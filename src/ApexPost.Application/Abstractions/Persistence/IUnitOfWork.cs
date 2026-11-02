namespace ApexPost.Application.Abstractions.Persistence;

public sealed class ConcurrencyConflictException(string message, Exception innerException)
    : InvalidOperationException(message, innerException);

public sealed class UniqueKeyConflictException(string message, Exception innerException)
    : InvalidOperationException(message, innerException);

public interface IUnitOfWork
{
    bool SupportsAtomicTransactions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
