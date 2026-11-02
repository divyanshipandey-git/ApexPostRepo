using System.Text.Json;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApexPost.Web.IntegrationTests;

public sealed class SafeExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_MapsRowVersionConflictToSafe409Problem()
    {
        var middleware = new SafeExceptionMiddleware(
            _ => throw new DbUpdateConcurrencyException("Provider details must not be returned."),
            NullLogger<SafeExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(body);
        Assert.Equal(StatusCodes.Status409Conflict, problem.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("Provider details", problem.RootElement.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_MapsApplicationConcurrencyConflictToSafe409Problem()
    {
        var middleware = new SafeExceptionMiddleware(
            _ => throw new ConcurrencyConflictException(
                "The record changed.",
                new InvalidOperationException("Provider details must not be returned.")),
            NullLogger<SafeExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(body);
        Assert.Equal(StatusCodes.Status409Conflict, problem.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("Provider details", problem.RootElement.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_MapsUniqueKeyConflictToSafe409Problem()
    {
        var middleware = new SafeExceptionMiddleware(
            _ => throw new UniqueKeyConflictException(
                "Duplicate key.",
                new InvalidOperationException("Database details must not be returned.")),
            NullLogger<SafeExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(body);
        Assert.Equal(StatusCodes.Status409Conflict, problem.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("Database details", problem.RootElement.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_MapsValidationExceptionToSafe400Problem()
    {
        var middleware = new SafeExceptionMiddleware(
            _ => throw new System.ComponentModel.DataAnnotations.ValidationException("Private validation message."),
            NullLogger<SafeExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(body);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("Private validation", problem.RootElement.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_MapsMissingResourceToSafe404Problem()
    {
        var middleware = new SafeExceptionMiddleware(
            _ => throw new KeyNotFoundException("Private resource key."),
            NullLogger<SafeExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(body);
        Assert.Equal(StatusCodes.Status404NotFound, problem.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("Private resource key", problem.RootElement.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotTranslateClientCancellationToServerError()
    {
        using var abort = new CancellationTokenSource();
        abort.Cancel();
        var middleware = new SafeExceptionMiddleware(
            _ => throw new OperationCanceledException(abort.Token),
            NullLogger<SafeExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.RequestAborted = abort.Token;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }
}
