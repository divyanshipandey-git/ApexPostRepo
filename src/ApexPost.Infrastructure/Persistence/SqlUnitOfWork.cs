using ApexPost.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

public sealed class SqlUnitOfWork(ApexPostDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception)
        {
            context.ChangeTracker.Clear();
            throw new ConcurrencyConflictException("The record was changed by another request. Retry the operation.", exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            context.ChangeTracker.Clear();
            throw new UniqueKeyConflictException("A unique constraint was violated.", exception);
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
                await context.SaveChangesAsync(cancellationToken);
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

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        // SQL Server error 2601: Cannot insert duplicate key row
        // SQL Server error 2627: Violation of UNIQUE KEY constraint
        return exception.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx
            && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }
}

/// <summary>
/// No-op implementation of <see cref="IUnitOfWork"/> for JSON import/test scenarios only.
/// Does NOT provide SQL transaction guarantees, concurrency handling, or audit persistence.
/// Use <see cref="SqlUnitOfWork"/> for all SQL-backed workflows.
/// </summary>
public sealed class NoOpUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default) =>
        await operation(cancellationToken);
}