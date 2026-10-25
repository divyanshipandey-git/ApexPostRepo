
namespace ApexPost.Infrastructure.Persistence;

public sealed record AuditLogEntry(
    DateTimeOffset Timestamp,
    string ActorId,
    string Action,
    string EntityType,
    string EntityId,
    string Outcome,
    string? CorrelationId = null);
