using System.Text.Json;
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
}
