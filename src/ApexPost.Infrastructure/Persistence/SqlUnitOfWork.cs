using ApexPost.Application.Abstractions.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

public sealed class SqlUnitOfWork(ApexPostDbContext context) : IUnitOfWork
{
    public bool SupportsAtomicTransactions => true;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception)
        {
            context.ChangeTracker.Clear();
            throw new ConcurrencyConflictException("The record was changed by another request. Retry the operation.", exception);
        }
        catch (DbUpdateException exception) when (IsUniqueKeyViolation(exception))
        {
            context.ChangeTracker.Clear();
            throw new UniqueKeyConflictException(
                "A record with the same unique key already exists.",
                exception);
        }
        catch { context.ChangeTracker.Clear(); throw; }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                await SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception operationException)
            {
                context.ChangeTracker.Clear();
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "The database operation failed and rollback could not be confirmed.",
                        operationException,
                        rollbackException);
                }

                throw;
            }
        });
    }

    private static bool IsUniqueKeyViolation(DbUpdateException exception)
    {
        for (var cause = exception.InnerException; cause is not null; cause = cause.InnerException)
        {
            if (cause is SqlException { Number: 2601 or 2627 })
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class NoOpUnitOfWork : IUnitOfWork
{
    public bool SupportsAtomicTransactions => false;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default) =>
        await operation(cancellationToken);
}
