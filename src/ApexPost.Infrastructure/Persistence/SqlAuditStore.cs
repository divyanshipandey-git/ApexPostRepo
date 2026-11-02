using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Services.Auditing;
using ApexPost.Infrastructure.Persistence.SqlEntities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

public sealed class SqlAuditStore(IDbContextFactory<ApexPostDbContext> contextFactory) : IAuditStore
{
    public async Task WriteAsync(AuditActionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.AuditEntries.Add(SqlEntityMapper.ToRecord(record));
        await context.SaveChangesAsync(cancellationToken);
    }
}
