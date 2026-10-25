using ApexPost.Application.Services.Auditing;

namespace ApexPost.Application.Abstractions.Persistence;

public interface IAuditStore
{
    Task WriteAsync(AuditActionRecord record, CancellationToken cancellationToken = default);
}
