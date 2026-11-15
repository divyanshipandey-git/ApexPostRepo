// COPILOT-ASSISTED: drafted with GitHub Copilot (prompt 5 in COPILOT-LOG.md), then reviewed by the student.

using System.Collections.Generic;
using ApexPost.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexPost.UnitTests.Api;

public class ApiKeyAuthorizeAttributeCopilotTests
{
    private const string TestKey = "test-key-123";

    [Fact]
    public async Task OnAuthorizationAsync_NoKeyConfiguredOnServer_Returns500ProblemDetails()
    {
        // Arrange
        Environment.SetEnvironmentVariable("APEXPOST_PARTNER_API_KEY", null);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var filterContext = CreateFilterContext(configuration);

        // Act
        await new ApiKeyAuthorizeAttribute().OnAuthorizationAsync(filterContext);

        // Assert
        var result = Assert.IsType<ObjectResult>(filterContext.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Server Configuration Error", problem.Title);
        Assert.Equal("Partner API key is not configured on the server", problem.Detail);
    }

    [Fact]
    public async Task OnAuthorizationAsync_MissingHeader_Returns401Unauthorized()
    {
        // Arrange
        Environment.SetEnvironmentVariable("APEXPOST_PARTNER_API_KEY", null);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PartnerApiKey"] = TestKey,
            })
            .Build();
        var filterContext = CreateFilterContext(configuration);

        // Act
        await new ApiKeyAuthorizeAttribute().OnAuthorizationAsync(filterContext);

        // Assert
        var result = Assert.IsType<ObjectResult>(filterContext.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Unauthorized", problem.Title);
        Assert.Equal($"Missing {ApiKeyAuthorizeAttribute.HeaderName} header", problem.Detail);
    }

    [Fact]
    public async Task OnAuthorizationAsync_EmptyHeader_Returns401Unauthorized()
    {
        // Arrange
        Environment.SetEnvironmentVariable("APEXPOST_PARTNER_API_KEY", null);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PartnerApiKey"] = TestKey,
            })
            .Build();
        var filterContext = CreateFilterContext(configuration, string.Empty);

        // Act
        await new ApiKeyAuthorizeAttribute().OnAuthorizationAsync(filterContext);

        // Assert
        var result = Assert.IsType<ObjectResult>(filterContext.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Unauthorized", problem.Title);
        Assert.Equal($"Missing {ApiKeyAuthorizeAttribute.HeaderName} header", problem.Detail);
    }

    [Fact]
    public async Task OnAuthorizationAsync_WrongKey_Returns401Unauthorized()
    {
        // Arrange
        Environment.SetEnvironmentVariable("APEXPOST_PARTNER_API_KEY", null);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PartnerApiKey"] = TestKey,
            })
            .Build();
        var filterContext = CreateFilterContext(configuration, "wrong-key");

        // Act
        await new ApiKeyAuthorizeAttribute().OnAuthorizationAsync(filterContext);

        // Assert
        var result = Assert.IsType<ObjectResult>(filterContext.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Unauthorized", problem.Title);
        Assert.Equal("Invalid API key", problem.Detail);
    }

    [Fact]
    public async Task OnAuthorizationAsync_PartnerApiKeyConfigured_AcceptsKeyAndLeavesResultNull()
    {
        // Arrange
        Environment.SetEnvironmentVariable("APEXPOST_PARTNER_API_KEY", null);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PartnerApiKey"] = TestKey,
            })
            .Build();
        var filterContext = CreateFilterContext(configuration, TestKey);

        // Act
        await new ApiKeyAuthorizeAttribute().OnAuthorizationAsync(filterContext);

        // Assert
        Assert.Null(filterContext.Result);
    }

    [Fact]
    public async Task OnAuthorizationAsync_PartnerApiKeysSectionConfigured_AcceptsKeyAndLeavesResultNull()
    {
        // Arrange
        Environment.SetEnvironmentVariable("APEXPOST_PARTNER_API_KEY", null);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PartnerApiKeys:SomePartner"] = TestKey,
            })
            .Build();
        var filterContext = CreateFilterContext(configuration, TestKey);

        // Act
        await new ApiKeyAuthorizeAttribute().OnAuthorizationAsync(filterContext);

        // Assert
        Assert.Null(filterContext.Result);
    }

    [Theory]
    [InlineData("test-key-12")]
    [InlineData("test-key-123-extra")]
    public async Task OnAuthorizationAsync_KeyIsPrefixOrLongerVersion_Returns401Unauthorized(string providedKey)
    {
        // Arrange
        Environment.SetEnvironmentVariable("APEXPOST_PARTNER_API_KEY", null);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PartnerApiKey"] = TestKey,
            })
            .Build();
        var filterContext = CreateFilterContext(configuration, providedKey);

        // Act
        await new ApiKeyAuthorizeAttribute().OnAuthorizationAsync(filterContext);

        // Assert
        var result = Assert.IsType<ObjectResult>(filterContext.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Unauthorized", problem.Title);
        Assert.Equal("Invalid API key", problem.Detail);
    }

    [Fact]
    public async Task OnAuthorizationAsync_EnvironmentVariableKeyConfigured_AcceptsKeyAndLeavesResultNull()
    {
        // Arrange
        var previous = Environment.GetEnvironmentVariable("APEXPOST_PARTNER_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("APEXPOST_PARTNER_API_KEY", TestKey);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();
            var filterContext = CreateFilterContext(configuration, TestKey);

            // Act
            await new ApiKeyAuthorizeAttribute().OnAuthorizationAsync(filterContext);

            // Assert
            Assert.Null(filterContext.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("APEXPOST_PARTNER_API_KEY", previous);
        }
    }

    private static AuthorizationFilterContext CreateFilterContext(IConfiguration configuration, string? headerValue = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .BuildServiceProvider();

        if (headerValue is not null)
        {
            httpContext.Request.Headers[ApiKeyAuthorizeAttribute.HeaderName] = headerValue;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }
}
