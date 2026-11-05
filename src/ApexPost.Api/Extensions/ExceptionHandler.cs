using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ApexPost.Api.Exceptions;

namespace ApexPost.Api.Extensions;

/// <summary>
/// Global exception handler that returns ProblemDetails for all errors.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title, detail, type) = MapException(exception);

        var problemDetails = new ProblemDetails
        {
            Type = type,
            Title = title,
            Detail = detail,
            Status = statusCode,
            Instance = httpContext.Request.Path
        };

        // Add trace ID for debugging
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        // Log the exception
        var logLevel = statusCode >= 500 ? LogLevel.Error : LogLevel.Warning;
        _logger.Log(logLevel, exception, "Exception handled: {ExceptionType} - {Message}", exception.GetType().Name, exception.Message);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title, string Detail, string Type) MapException(Exception exception)
    {
        return exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource Not Found", exception.Message, "https://tools.ietf.org/html/rfc7231#section-6.5.4"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message, "https://tools.ietf.org/html/rfc7231#section-6.5.8"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation Error", exception.Message, "https://tools.ietf.org/html/rfc7231#section-6.5.1"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid Argument", exception.Message, "https://tools.ietf.org/html/rfc7231#section-6.5.1"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", exception.Message, "https://tools.ietf.org/html/rfc7235#section-3.1"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred", "https://tools.ietf.org/html/rfc7231#section-6.6.1")
        };
    }
}