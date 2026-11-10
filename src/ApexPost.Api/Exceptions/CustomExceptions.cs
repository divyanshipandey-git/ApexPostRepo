namespace ApexPost.Api.Exceptions;

/// <summary>
/// Exception thrown when a resource is not found.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when a conflict occurs (duplicate, invalid state transition, etc.).
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when the caller is authenticated but not allowed to perform the operation
/// (for example uploading scans that belong to a different courier).
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
