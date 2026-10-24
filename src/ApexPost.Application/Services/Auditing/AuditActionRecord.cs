namespace ApexPost.Application.Services.Auditing;

public sealed record AuditActionRecord(
    DateTimeOffset Timestamp,
    string? StaffId,
    string? Role,
    string? BranchCode,
    string Controller,
    string Action,
    IReadOnlyDictionary<string, string?> RouteValues,
    int StatusCode,
    long DurationMilliseconds,
    string? CorrelationId);
