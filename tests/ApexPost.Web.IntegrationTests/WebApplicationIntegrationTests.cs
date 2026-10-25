using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Notifications;
using ApexPost.Application.Services.Notifications;
using ApexPost.Application.Services.Inventory;
using ApexPost.Application.Abstractions.Security;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text.RegularExpressions;
using System.Net.Http.Json;
using ApexPost.Web.Models.Shipping;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApexPost.Web.IntegrationTests;

public sealed class WebApplicationIntegrationTests
{
    [Fact]
    public async Task InventoryPageLoadsWithSearchForm()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = await factory.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/Inventory/Index");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Inventory inquiry", content);
        Assert.Contains("name=\"BranchCode\"", content);
        Assert.Contains("name=\"Sku\"", content);
        Assert.Contains("Check stock", content);
        Assert.Contains("site.css", content);
        Assert.Contains("Staff navigation", content);
        Assert.Contains("Signed in as", content);
        Assert.Contains("staff-test-1", content);
        Assert.Contains("aria-current=\"page\"", content);
        Assert.Contains("Sign out", content);
        Assert.Contains("id=\"theme-toggle\"", content);
        Assert.Contains("aria-label=\"Switch to dark theme\"", content);
        Assert.Contains("class=\"form-control text-input\"", content);
        Assert.Contains("class=\"card shadow-sm surface-card inquiry-form\"", content);
        Assert.DoesNotContain("Counter Transactions", content);
        Assert.DoesNotContain("Money Orders", content);

        using var stylesheet = await client.GetAsync("/css/site.css");
        var css = await stylesheet.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, stylesheet.StatusCode);
        Assert.Contains("@media (max-width: 760px)", css, StringComparison.Ordinal);
        Assert.Contains(":focus-visible", css, StringComparison.Ordinal);
        Assert.Contains("[data-bs-theme=\"dark\"]", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuccessfulInquiryPostUsesPrgAndDisplaysInventory()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedInventoryAsync();
        using var client = await factory.CreateAuthenticatedClientAsync(
            options: new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var form = await CreateInquiryFormAsync(client, "ind01", "env001");
        using var postResponse = await client.PostAsync("/Inventory/Search", form);

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.NotNull(postResponse.Headers.Location);
        Assert.Contains("branchCode=IND01", postResponse.Headers.Location!.ToString());
        Assert.Contains("sku=ENV001", postResponse.Headers.Location.ToString());

        using var getResponse = await client.GetAsync(postResponse.Headers.Location);
        var content = await getResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains("Central Branch", content);
        Assert.Contains("IND01", content);
        Assert.Contains("Envelope", content);
        Assert.Contains("ENV001", content);
        Assert.Contains("<dd>12</dd>", content);
    }

    [Theory]
    [InlineData("", "ENV001", "Enter a branch code.")]
    [InlineData("IND01", "", "Enter a SKU.")]
    [InlineData("IND-01", "ENV001", "Branch code must contain only letters and digits")]
    [InlineData("IND01", "ENV-001", "SKU must contain only letters and digits")]
    [InlineData("ABCDEFGHIJK", "ENV001", "Branch code must be no longer than 10 characters.")]
    [InlineData("IND01", "ABCDEFGHIJKLMNOPQRSTU", "SKU must be no longer than 20 characters.")]
    public async Task InvalidOrMissingInputRedisplaysValidationErrors(
        string branchCode,
        string sku,
        string expectedMessage)
    {
        using var factory = new TestWebApplicationFactory();
        using var client = await factory.CreateAuthenticatedClientAsync();

        using var form = await CreateInquiryFormAsync(client, branchCode, sku);
        using var response = await client.PostAsync("/Inventory/Search", form);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedMessage, content);
        Assert.Contains("Inventory inquiry", content);
        Assert.Contains($"value=\"{branchCode}\"", content);
        Assert.Contains($"value=\"{sku}\"", content);
    }

    [Fact]
    public async Task SearchPostWithoutAntiforgeryTokenIsRejected()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = await factory.CreateAuthenticatedClientAsync();

        using var response = await client.PostAsync(
            "/Inventory/Search",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["BranchCode"] = "IND01",
                ["Sku"] = "ENV001"
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownBranchDisplaysFriendlyNotFoundMessage()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedInventoryAsync();
        using var client = await factory.CreateAuthenticatedClientAsync(StaffRole.Administrator);

        using var response = await SubmitInquiryAsync(client, "IND99", "ENV001");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var visibleContent = WebUtility.HtmlDecode(content);
        Assert.Contains("Branch 'IND99' was not found.", visibleContent);
        Assert.DoesNotContain("System.", visibleContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownProductDisplaysFriendlyNotFoundMessage()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedInventoryAsync();
        using var client = await factory.CreateAuthenticatedClientAsync();

        using var response = await SubmitInquiryAsync(client, "IND01", "UNKNOWN");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var visibleContent = WebUtility.HtmlDecode(content);
        Assert.Contains("Product with SKU 'UNKNOWN' was not found.", visibleContent);
        Assert.DoesNotContain("StackTrace", visibleContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingStockRecordDisplaysFriendlyNotFoundMessage()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedInventoryAsync(includeStock: false);
        using var client = await factory.CreateAuthenticatedClientAsync();

        using var response = await SubmitInquiryAsync(client, "IND01", "ENV001");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var visibleContent = WebUtility.HtmlDecode(content);
        Assert.Contains(
            "No stock record was found for branch 'IND01' and SKU 'ENV001'.",
            visibleContent);
    }

    [Fact]
    public async Task LoginPageRendersValidatedAntiforgeryForm()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync("/Account/Login");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"Email\"", content);
        Assert.Contains("name=\"Password\"", content);
        Assert.Contains("__RequestVerificationToken", content);
        Assert.Contains("login-card", content);
        Assert.Contains("id=\"theme-toggle\"", content);
        Assert.Contains("aria-pressed=\"false\"", content);
        Assert.Contains("id=\"password-toggle\"", content);
        Assert.Contains("aria-controls=\"Password\"", content);
        Assert.Contains("/js/login.js", content);
        Assert.DoesNotContain("href=\"/Inventory/Index\"", content);
    }

    [Fact]
    public async Task ThemeBootstrapAssetsAndEarlyThemeInitializationAreAvailableLocally()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var page = await client.GetAsync("/Account/Login");
        var html = await page.Content.ReadAsStringAsync();
        using var bootstrap = await client.GetAsync("/lib/bootstrap/bootstrap.min.css");
        using var siteCss = await client.GetAsync("/css/site.css");
        using var themeScript = await client.GetAsync("/js/theme.js");
        using var loginScript = await client.GetAsync("/js/login.js");
        var bootstrapCss = await bootstrap.Content.ReadAsStringAsync();
        var applicationCss = await siteCss.Content.ReadAsStringAsync();
        var script = await themeScript.Content.ReadAsStringAsync();
        var login = await loginScript.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode);
        Assert.Equal(HttpStatusCode.OK, siteCss.StatusCode);
        Assert.Equal(HttpStatusCode.OK, themeScript.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginScript.StatusCode);
        Assert.Contains("Bootstrap  v5.3.3", bootstrapCss, StringComparison.Ordinal);
        Assert.Contains("--postal-orange", applicationCss, StringComparison.Ordinal);
        Assert.Contains("[data-bs-theme=\"dark\"] .login-shell", applicationCss, StringComparison.Ordinal);
        Assert.Contains("localStorage.getItem", html, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf("localStorage.getItem", StringComparison.Ordinal)
            < html.IndexOf("bootstrap.min.css", StringComparison.Ordinal),
            "Theme initialization must execute before the stylesheets load.");
        Assert.Contains("matchMedia", html, StringComparison.Ordinal);
        Assert.Contains("localStorage.setItem", script, StringComparison.Ordinal);
        Assert.Contains("aria-pressed", script, StringComparison.Ordinal);
        Assert.Contains("addEventListener(\"storage\"", script, StringComparison.Ordinal);
        Assert.Contains("password.type = reveal ? \"text\" : \"password\"", login, StringComparison.Ordinal);
        Assert.Contains("aria-busy", login, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JsonFilesInConfiguredDataDirectoryAreNotPubliclyServed()
    {
        using var factory = new TestWebApplicationFactory();
        const string marker = "private-integration-data-marker";
        await File.WriteAllTextAsync(
            Path.Combine(factory.DataDirectory, "staff.json"),
            $$"""[{"marker":"{{marker}}"}]""");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var direct = await client.GetAsync("/staff.json");
        using var appData = await client.GetAsync("/App_Data/staff.json");
        using var audit = await client.GetAsync("/audit.jsonl");

        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, appData.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, audit.StatusCode);
        Assert.DoesNotContain(marker, await direct.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain(marker, await appData.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidCredentialsShowGenericErrorWithoutEchoingPassword()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedStaffAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");

        using var response = await PostLoginAsync(client, token, "nobody@example.test", "Wrong-secret-42!");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid email address or password.", WebUtility.HtmlDecode(content));
        Assert.DoesNotContain("Wrong-secret-42!", content, StringComparison.Ordinal);
        Assert.DoesNotContain("account was not found", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidCredentialsIssueSecureCookieWithStaffClaimsAndAuthorizeInventory()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedStaffAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");

        using var login = await PostLoginAsync(
            client,
            token,
            TestWebApplicationFactory.TestEmail,
            TestWebApplicationFactory.TestPassword);
        var setCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        var cookieValue = setCookie.Split(';', 2)[0].Split('=', 2)[1];
        var cookieOptions = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = cookieOptions.TicketDataFormat.Unprotect(Uri.UnescapeDataString(cookieValue));
        using var inventory = await client.GetAsync("/Inventory/Index");
        var credentialJson = await File.ReadAllTextAsync(
            Path.Combine(factory.DataDirectory, "staff-credentials.json"));

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.NotNull(ticket);
        Assert.Contains(ticket!.Principal.Claims, claim => claim.Type == "StaffId" && claim.Value == "staff-test-1");
        Assert.Contains(ticket.Principal.Claims, claim => claim.Type == "Role" && claim.Value == nameof(StaffRole.CounterClerk));
        Assert.Contains(ticket.Principal.Claims, claim => claim.Type == "BranchCode" && claim.Value == "IND01");
        Assert.Contains("__Host-ApexPost.Auth", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pbkdf2-sha256$600000$", credentialJson, StringComparison.Ordinal);
        Assert.DoesNotContain(TestWebApplicationFactory.TestPassword, credentialJson, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, inventory.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedInventoryRequestRedirectsToLogin()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/Inventory/Index");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
    }

    [Theory]
    [InlineData(StaffRole.CounterClerk, "IND01", HttpStatusCode.OK)]
    [InlineData(StaffRole.BranchManager, "IND01", HttpStatusCode.OK)]
    [InlineData(StaffRole.Auditor, "IND01", HttpStatusCode.Redirect)]
    [InlineData(StaffRole.CounterClerk, "IND02", HttpStatusCode.Redirect)]
    public async Task InventoryAuthorizationEnforcesRoleAndBranch(
        StaffRole role,
        string requestedBranch,
        HttpStatusCode expectedStatus)
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedStaffAsync(role, "IND01");
        await factory.SeedInventoryAsync();
        using var client = await factory.CreateAuthenticatedClientAsync(role, "IND01");

        using var response = await client.GetAsync($"/Inventory/Index?branchCode={requestedBranch}&sku=ENV001");

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.Redirect)
        {
            Assert.Equal("/Account/AccessDenied", new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
        }
    }

    [Fact]
    public async Task LogoutRequiresAntiforgeryAndClearsAuthenticationCookie()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = await factory.CreateAuthenticatedClientAsync();
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");

        using var logoutWithoutToken = await client.PostAsync(
            "/Account/Logout",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        using var logout = await client.PostAsync(
            "/Account/Logout",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));
        using var protectedPage = await client.GetAsync("/Inventory/Index");

        Assert.Equal(HttpStatusCode.BadRequest, logoutWithoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.Found, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
        Assert.Equal("/Account/Login", new Uri(client.BaseAddress!, protectedPage.Headers.Location!).AbsolutePath);
    }

    [Fact]
    public async Task LoginRateLimitReturnsTooManyRequestsAfterFivePosts()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedStaffAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await PostLoginAsync(client, token, "nobody@example.test", "Bad-secret-42!");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var limited = await PostLoginAsync(client, token, "nobody@example.test", "Bad-secret-42!");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task NonDevelopmentErrorRouteRendersSafeMessage()
    {
        using var factory = new TestWebApplicationFactory(environment: "Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync("/Home/Error");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Something went wrong", content);
        Assert.DoesNotContain("StackTrace", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrossCuttingHeadersAndCorrelationIdAreApplied()
    {
        using var factory = new TestWebApplicationFactory();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        Assert.True(bool.Parse(configuration["Logging:Console:FormatterOptions:IncludeScopes"]!));

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "integration-check-42");

        using var response = await client.GetAsync("/Account/Login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("integration-check-42", Assert.Single(response.Headers.GetValues("X-Correlation-Id")));
        var contentSecurityPolicy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", contentSecurityPolicy);
        Assert.Contains("nonce-", contentSecurityPolicy);
        var nonce = Regex.Match(contentSecurityPolicy, "nonce-(?<nonce>[^']+)").Groups["nonce"].Value;
        Assert.Contains($"nonce=\"{nonce}\"", WebUtility.HtmlDecode(html));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));

        client.DefaultRequestHeaders.Remove("X-Correlation-Id");
        using var generatedIdResponse = await client.GetAsync("/health");
        Assert.Matches("^[0-9a-f]{32}$", Assert.Single(generatedIdResponse.Headers.GetValues("X-Correlation-Id")));
    }

    [Fact]
    public async Task UnhandledExceptionReturnsSafeProblemDetailsAndTraceIdentifiers()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "safe-error-check");

        using var response = await client.GetAsync("/test-probes/throw");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("An unexpected error occurred.", body);
        Assert.Contains("safe-error-check", body);
        Assert.DoesNotContain("private-diagnostic-detail", body);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MaintenanceModeReturns503ButLeavesHealthEndpointAvailable()
    {
        using var factory = new TestWebApplicationFactory(maintenanceEnabled: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var maintenance = await client.GetAsync("/Account/Login");
        var body = await maintenance.Content.ReadAsStringAsync();
        using var health = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, maintenance.StatusCode);
        Assert.Equal("3600", Assert.Single(maintenance.Headers.GetValues("Retry-After")));
        Assert.Contains("temporarily unavailable", body);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task ApiKeyMetadataRejectsMissingAndInvalidKeys()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var missing = await client.GetAsync("/test-probes/api-key");
        client.DefaultRequestHeaders.Add("X-Api-Key", "not-the-test-key");
        using var invalid = await client.GetAsync("/test-probes/api-key");

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    [Fact]
    public async Task ApiKeyMetadataReturnsConfigurationFailureWhenServerKeyIsMissing()
    {
        using var factory = new TestWebApplicationFactory(configureIntegrationKey: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Api-Key", TestWebApplicationFactory.TestApiKey);

        using var response = await client.GetAsync("/test-probes/api-key");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("TestApiKey", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task IpAllowlistMetadataRejectsBlockedRemoteAddress()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Api-Key", TestWebApplicationFactory.TestApiKey);

        using var response = await client.GetAsync("/test-probes/api-key");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuditFilterPersistsActionMetadataWithoutCredentialsOrRequestBody()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = await factory.CreateAuthenticatedClientAsync();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "audit-correlation-7");

        using var response = await client.GetAsync("/Inventory/Index");
        var audit = await File.ReadAllTextAsync(Path.Combine(factory.DataDirectory, "audit.jsonl"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("staff-test-1", audit);
        Assert.Contains("CounterClerk", audit);
        Assert.Contains("IND01", audit);
        Assert.Contains("Inventory", audit);
        Assert.Contains("Index", audit);
        Assert.Contains("audit-correlation-7", audit);
        Assert.Contains("statusCode", audit);
        Assert.Contains("durationMilliseconds", audit);
        Assert.DoesNotContain(TestWebApplicationFactory.TestPassword, audit);
        Assert.DoesNotContain("__RequestVerificationToken", audit);
    }

    [Fact]
    public async Task CourierStatusEndpointUpdatesAndPersistsShipmentStatus()
    {
        using var factory = new TestWebApplicationFactory(
            allowedIntegrationIps: ["127.0.0.1", "::1"]);
        await factory.SeedShipmentAsync("SHP-COURIER-1");
        using var client = CreateCourierClient(factory);

        using var response = await PostCourierStatusAsync(client, "SHP-COURIER-1", "In transit");
        var content = await response.Content.ReadFromJsonAsync<CourierStatusUpdateResponse>();
        var saved = await factory.Services.GetRequiredService<IShipmentRepository>()
            .GetByIdAsync("SHP-COURIER-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.Equal("SHP-COURIER-1", content.ShipmentId);
        Assert.Equal("In transit", content.Status);
        Assert.NotNull(saved);
        Assert.Equal("In transit", saved.CourierStatus);
        Assert.Equal(content.ReceivedAtUtc, saved.CourierStatusUpdatedAtUtc);
        Assert.Contains("In transit", await File.ReadAllTextAsync(Path.Combine(factory.DataDirectory, "shipments.json")));
        var audit = await File.ReadAllTextAsync(Path.Combine(factory.DataDirectory, "audit.jsonl"));
        Assert.Contains("CourierIntegration", audit);
        Assert.Contains("UpdateShipmentStatus", audit);
        Assert.DoesNotContain(TestWebApplicationFactory.TestApiKey, audit);
    }

    [Fact]
    public async Task CourierStatusEndpointReturnsNotFoundForUnknownShipment()
    {
        using var factory = new TestWebApplicationFactory(allowedIntegrationIps: ["127.0.0.1", "::1"]);
        using var client = CreateCourierClient(factory);

        using var response = await PostCourierStatusAsync(client, "SHP-MISSING", "Delivered");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Shipment was not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CourierStatusEndpointRejectsInvalidInputWithoutChangingShipment()
    {
        using var factory = new TestWebApplicationFactory(allowedIntegrationIps: ["127.0.0.1", "::1"]);
        await factory.SeedShipmentAsync("SHP-INVALID-1");
        using var client = CreateCourierClient(factory);

        using var response = await PostCourierStatusAsync(client, "SHP-INVALID-1", "  ");
        var saved = await factory.Services.GetRequiredService<IShipmentRepository>()
            .GetByIdAsync("SHP-INVALID-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(saved);
        Assert.Null(saved.CourierStatus);
    }

    [Fact]
    public async Task CourierStatusEndpointRejectsMissingAndInvalidApiKeys()
    {
        using var factory = new TestWebApplicationFactory(allowedIntegrationIps: ["127.0.0.1", "::1"]);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var missing = await PostCourierStatusAsync(client, "SHP-ANY", "Delivered", apiKey: null);
        using var invalid = await PostCourierStatusAsync(client, "SHP-ANY", "Delivered", apiKey: "incorrect-key");

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    [Fact]
    public async Task CourierStatusEndpointFailsClosedWhenServerApiKeyIsMissing()
    {
        using var factory = new TestWebApplicationFactory(
            configureIntegrationKey: false,
            allowedIntegrationIps: ["127.0.0.1", "::1"]);
        using var client = CreateCourierClient(factory);

        using var response = await PostCourierStatusAsync(
            client,
            "SHP-ANY",
            "Delivered",
            apiKey: TestWebApplicationFactory.TestApiKey);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("TestApiKey", body);
        Assert.DoesNotContain(TestWebApplicationFactory.TestApiKey, body);
    }

    [Fact]
    public async Task CourierStatusEndpointRejectsBlockedClientIp()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedShipmentAsync("SHP-BLOCKED-1");
        using var client = CreateCourierClient(factory);

        using var response = await PostCourierStatusAsync(client, "SHP-BLOCKED-1", "Delivered");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var saved = await factory.Services.GetRequiredService<IShipmentRepository>()
            .GetByIdAsync("SHP-BLOCKED-1");
        Assert.Null(saved!.CourierStatus);
    }

    [Fact]
    public async Task CourierStatusEndpointHonorsApprovedAndUnapprovedCorsOrigins()
    {
        using var factory = new TestWebApplicationFactory(allowedIntegrationIps: ["127.0.0.1", "::1"]);
        await factory.SeedShipmentAsync("SHP-CORS-1");
        using var client = CreateCourierClient(factory);

        using var approved = await PostCourierStatusAsync(
            client,
            "SHP-CORS-1",
            "Arrived at depot",
            origin: "https://courier.apexpost.test");
        using var unapproved = await PostCourierStatusAsync(
            client,
            "SHP-CORS-1",
            "Out for delivery",
            origin: "https://untrusted.example");

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("https://courier.apexpost.test", Assert.Single(approved.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal(HttpStatusCode.OK, unapproved.StatusCode);
        Assert.False(unapproved.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task CourierStatusEndpointIsRateLimitedIndependently()
    {
        using var factory = new TestWebApplicationFactory(allowedIntegrationIps: ["127.0.0.1", "::1"]);
        await factory.SeedShipmentAsync("SHP-RATE-1");
        using var client = CreateCourierClient(factory);

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 61; attempt++)
        {
            last?.Dispose();
            last = await PostCourierStatusAsync(client, "SHP-RATE-1", "In transit");
        }

        using (last)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        }
    }

    [Fact]
    public async Task BranchManagerCanVoidPaidTransactionAndPersistsThroughPrg()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedTransactionAsync("TXN-VOID-WEB-1");
        using var client = await factory.CreateAuthenticatedClientAsync(StaffRole.BranchManager);
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Details/TXN-VOID-WEB-1");

        using var post = await PostVoidAsync(client, token, "TXN-VOID-WEB-1", "Duplicate sale");
        var saved = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByIdAsync("TXN-VOID-WEB-1");

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Contains("/Transactions/Details/TXN-VOID-WEB-1", post.Headers.Location!.ToString());
        Assert.NotNull(saved);
        Assert.Equal(TransactionStatus.Voided, saved.Status);
        Assert.Equal("Duplicate sale", saved.Reason);

        using var redirected = await client.GetAsync(post.Headers.Location);
        var page = await redirected.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, redirected.StatusCode);
        Assert.Contains("Voided", page);
        Assert.Contains("Duplicate sale", page);
        Assert.DoesNotContain("Void transaction", page);
        Assert.Contains(factory.Services.GetRequiredService<SimulatedNotificationService>()
            .GetPublishedNotifications(), notification => notification.Type == NotificationType.TransactionVoid);

        var audit = await File.ReadAllTextAsync(Path.Combine(factory.DataDirectory, "audit.jsonl"));
        Assert.Contains("Transactions", audit);
        Assert.Contains("Void", audit);
        Assert.Contains("BranchManager", audit);
        Assert.DoesNotContain("Duplicate sale", audit);
    }

    [Fact]
    public async Task DeactivatedManagerCookieCannotVoidTransaction()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedTransactionAsync("TXN-DEACTIVATED-MANAGER-1");
        using var client = await factory.CreateAuthenticatedClientAsync(StaffRole.BranchManager);
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Details/TXN-DEACTIVATED-MANAGER-1");
        var staffRepository = factory.Services.GetRequiredService<IStaffRepository>();
        var staff = await staffRepository.GetByIdAsync("staff-test-1");
        staff!.Deactivate();
        await staffRepository.UpdateAsync(staff);

        using var response = await PostVoidAsync(client, token, "TXN-DEACTIVATED-MANAGER-1", "Inactive account");
        var saved = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByIdAsync("TXN-DEACTIVATED-MANAGER-1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TransactionStatus.Paid, saved!.Status);
    }

    [Fact]
    public async Task ChangedManagerRoleCookieCannotVoidTransaction()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedTransactionAsync("TXN-ROLE-CHANGED-1");
        using var client = await factory.CreateAuthenticatedClientAsync(StaffRole.BranchManager);
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Details/TXN-ROLE-CHANGED-1");
        await factory.Services.GetRequiredService<IStaffRepository>().UpdateAsync(new Staff(
            "staff-test-1",
            "Test Clerk",
            TestWebApplicationFactory.TestEmail,
            StaffRole.CounterClerk,
            new BranchCode("IND01")));
        using var response = await PostVoidAsync(client, token, "TXN-ROLE-CHANGED-1", "Stale role");
        var saved = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByIdAsync("TXN-ROLE-CHANGED-1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TransactionStatus.Paid, saved!.Status);
    }

    [Fact]
    public async Task ChangedManagerBranchCookieCannotVoidFormerBranchTransaction()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedTransactionAsync("TXN-BRANCH-CHANGED-1");
        using var client = await factory.CreateAuthenticatedClientAsync(StaffRole.BranchManager);
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Details/TXN-BRANCH-CHANGED-1");
        await factory.Services.GetRequiredService<IStaffRepository>().UpdateAsync(new Staff(
            "staff-test-1",
            "Test Clerk",
            TestWebApplicationFactory.TestEmail,
            StaffRole.BranchManager,
            new BranchCode("IND02")));
        using var response = await PostVoidAsync(client, token, "TXN-BRANCH-CHANGED-1", "Stale branch");
        var saved = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByIdAsync("TXN-BRANCH-CHANGED-1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TransactionStatus.Paid, saved!.Status);
    }

    [Fact]
    public async Task AdministratorCanVoidTransactionFromAnotherBranch()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedTransactionAsync("TXN-ADMIN-CROSS-BRANCH-1", "IND02");
        using var client = await factory.CreateAuthenticatedClientAsync(StaffRole.Administrator);
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Details/TXN-ADMIN-CROSS-BRANCH-1");

        using var response = await PostVoidAsync(client, token, "TXN-ADMIN-CROSS-BRANCH-1", "Admin correction");
        var saved = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByIdAsync("TXN-ADMIN-CROSS-BRANCH-1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(TransactionStatus.Voided, saved!.Status);
    }

    [Fact]
    public async Task BranchManagerCanRecordFullRefundWithReasonAndNotificationThroughPrg()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedTransactionAsync("TXN-REFUND-WEB-1");
        using var client = await factory.CreateAuthenticatedClientAsync(StaffRole.BranchManager);
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Details/TXN-REFUND-WEB-1");

        using var response = await PostRefundAsync(client, token, "TXN-REFUND-WEB-1", "Customer return");
        var saved = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByIdAsync("TXN-REFUND-WEB-1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Transactions/Details/TXN-REFUND-WEB-1", response.Headers.Location!.ToString());
        Assert.Equal(TransactionStatus.Refunded, saved!.Status);
        Assert.Equal("Customer return", saved.Reason);
        using var details = await client.GetAsync(response.Headers.Location);
        Assert.Contains("Recorded reason", await details.Content.ReadAsStringAsync());
        Assert.Contains(factory.Services.GetRequiredService<SimulatedNotificationService>()
            .GetPublishedNotifications(), notification => notification.Type == NotificationType.TransactionRefund);
    }

    [Fact]
    public async Task ClerkCannotRefundAndInvalidStateDoesNotMutate()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedTransactionAsync("TXN-REFUND-CLERK-1");
        using var clerk = await factory.CreateAuthenticatedClientAsync(StaffRole.CounterClerk);
        var clerkToken = await GetAntiforgeryTokenAsync(clerk, "/Account/Login");
        using var forbidden = await PostRefundAsync(clerk, clerkToken, "TXN-REFUND-CLERK-1", "Not allowed");
        var paid = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByIdAsync("TXN-REFUND-CLERK-1");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(TransactionStatus.Paid, paid!.Status);

        using var stateFactory = new TestWebApplicationFactory();
        await stateFactory.SeedTransactionAsync("TXN-REFUND-VOIDED-1", status: TransactionStatus.Voided);
        using var manager = await stateFactory.CreateAuthenticatedClientAsync(StaffRole.BranchManager);
        var managerToken = await GetAntiforgeryTokenAsync(manager, "/Transactions/Details/TXN-REFUND-VOIDED-1");
        using var conflict = await PostRefundAsync(manager, managerToken, "TXN-REFUND-VOIDED-1", "Already voided");
        var voided = await stateFactory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByIdAsync("TXN-REFUND-VOIDED-1");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(TransactionStatus.Voided, voided!.Status);
    }

    [Fact]
    public async Task CounterSalePageRendersProductCounterAndShippingOptions()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedCounterSaleDataAsync();
        using var client = await factory.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/Transactions/Create");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("New counter transaction", html);
        Assert.Contains("Complete transaction", html);
        Assert.Contains("Central Branch counter", html);
        Assert.Contains("Envelope (ENV001)", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("Shipping service (optional)", html);
        Assert.Contains("Money order (optional)", html);
        Assert.Contains("Identity check, when required", html);
    }

    [Fact]
    public async Task CounterSaleSubmitsMixedRetailAndShippingItemsWithPrgAndPersistsStock()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedCounterSaleDataAsync(stockQuantity: 5);
        using var client = await factory.CreateAuthenticatedClientAsync(
            options: new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Create");

        using var post = await client.PostAsync("/Transactions/Create", CreateCounterSaleForm(token,
            ("Products[0].Sku", "ENV001"),
            ("Products[0].Quantity", "2"),
            ("Products[0].UnitPrice", "0"),
            ("Total", "0"),
            ("BranchCode", "IND02"),
            ("StaffId", "admin-controlled-by-client"),
            ("Role", "Administrator"),
            ("IncludeShipping", "true"),
            ("ItemType", "Parcel"),
            ("WeightKg", "1.5"),
            ("LengthCm", "20"),
            ("WidthCm", "15"),
            ("HeightCm", "10"),
            ("DestinationZone", "ZONE1"),
            ("ShippingServiceLevel", "Standard")));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Contains("/Transactions/Receipt/", post.Headers.Location!.ToString());
        var redirectUri = post.Headers.Location;
        using var receiptResponse = await client.GetAsync(redirectUri);
        var receipt = await receiptResponse.Content.ReadAsStringAsync();
        var transactions = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByBranchAsync("IND01");
        var stock = await factory.Services.GetRequiredService<IInventoryRepository>()
            .GetByBranchAndSkuAsync("IND01", "ENV001");
        var shipmentRepository = factory.Services.GetRequiredService<IShipmentRepository>();
        var transaction = Assert.Single(transactions);
        var shippingLine = Assert.Single(transaction.LineItems, line => line.ItemType == "Service");
        var shipment = await shipmentRepository.GetByIdAsync(shippingLine.ReferenceId!);

        Assert.Equal(HttpStatusCode.OK, receiptResponse.StatusCode);
        Assert.Contains("Transaction receipt", receipt);
        Assert.Contains("Paid", receipt);
        Assert.Contains("Cash", receipt);
        Assert.Contains("Envelope", receipt);
        Assert.Contains("Shipping", receipt);
        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal("IND01", transaction.BranchCode.Value);
        Assert.Equal("staff-test-1", transaction.ClerkId);
        Assert.Equal(PaymentMethod.Cash, transaction.PaymentMethod);
        var retailLine = Assert.Single(transaction.LineItems, line => line.ItemType == "Retail");
        Assert.Equal(2, retailLine.Quantity);
        Assert.Equal(50m, retailLine.UnitPrice.Amount);
        Assert.Equal(170m, transaction.Subtotal.Amount);
        Assert.Equal(30.6m, transaction.Tax.Amount);
        Assert.Equal(200.6m, transaction.Total.Amount);
        Assert.Equal(3, stock!.Quantity);
        Assert.NotNull(shipment);
        Assert.Contains("Create", await File.ReadAllTextAsync(Path.Combine(factory.DataDirectory, "audit.jsonl")));
    }

    [Fact]
    public async Task HighValueMoneyOrderWithIdentityVerificationIsRecordedWithPaidTransactionAndPrg()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedCounterSaleDataAsync();
        using var client = await factory.CreateAuthenticatedClientAsync(
            options: new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Create");

        using var response = await client.PostAsync("/Transactions/Create", CreateCounterSaleForm(token,
            ("Products[0].Sku", ""),
            ("Products[0].Quantity", ""),
            ("IncludeMoneyOrder", "true"),
            ("MoneyOrderAmount", "15000"),
            ("MoneyOrderSenderName", "Test Sender"),
            ("MoneyOrderRecipientName", "Test Recipient"),
            ("IdentityDocumentType", "Government ID"),
            ("IdentityMaskedDocumentNumber", "XXXX1234"),
            ("IdentityVerified", "true")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Transactions/Receipt/", response.Headers.Location!.ToString());
        var transactions = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByBranchAsync("IND01");
        var transaction = Assert.Single(transactions);
        var moneyOrders = await factory.Services.GetRequiredService<IMoneyOrderRepository>()
            .GetByBranchAsync("IND01");
        var moneyOrder = Assert.Single(moneyOrders);

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(transaction.Id, moneyOrder.TransactionId);
        Assert.Equal("IND01", moneyOrder.BranchCode);
        Assert.Equal(15_000m, moneyOrder.Amount.Amount);
        Assert.Equal(10m, moneyOrder.Fee.Amount);
        Assert.True(moneyOrder.IdentityVerification!.IsVerified);
        Assert.Equal("XXXX1234", moneyOrder.IdentityVerification.MaskedDocumentNumber);

        using var receipt = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        Assert.Contains("Money order", await receipt.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HighValueMoneyOrderWithoutSuccessfulVerificationDoesNotPersistAnything()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedCounterSaleDataAsync();
        using var client = await factory.CreateAuthenticatedClientAsync();
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Create");

        using var response = await client.PostAsync("/Transactions/Create", CreateCounterSaleForm(token,
            ("Products[0].Sku", ""),
            ("Products[0].Quantity", ""),
            ("IncludeMoneyOrder", "true"),
            ("MoneyOrderAmount", "15000"),
            ("MoneyOrderSenderName", "Test Sender"),
            ("MoneyOrderRecipientName", "Test Recipient")));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("requires successful identity verification", html);
        Assert.Empty(await factory.Services.GetRequiredService<IMoneyOrderRepository>()
            .GetByBranchAsync("IND01"));
        Assert.Empty(await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByBranchAsync("IND01"));
    }

    [Fact]
    public async Task MoneyOrderRejectsUnmaskedIdentityDocumentNumberWithoutPersistence()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedCounterSaleDataAsync();
        using var client = await factory.CreateAuthenticatedClientAsync();
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Create");

        using var response = await client.PostAsync("/Transactions/Create", CreateCounterSaleForm(token,
            ("Products[0].Sku", ""),
            ("Products[0].Quantity", ""),
            ("IncludeMoneyOrder", "true"),
            ("MoneyOrderAmount", "100"),
            ("MoneyOrderSenderName", "Test Sender"),
            ("MoneyOrderRecipientName", "Test Recipient"),
            ("IdentityDocumentType", "Government ID"),
            ("IdentityMaskedDocumentNumber", "123456789012"),
            ("IdentityVerified", "true")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await factory.Services.GetRequiredService<IMoneyOrderRepository>()
            .GetByBranchAsync("IND01"));
        Assert.Empty(await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByBranchAsync("IND01"));
    }

    [Fact]
    public async Task CounterSaleRejectsInvalidProductAndPreservesStockAndFormInput()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedCounterSaleDataAsync(stockQuantity: 5);
        using var client = await factory.CreateAuthenticatedClientAsync();
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Create");

        using var response = await client.PostAsync("/Transactions/Create", CreateCounterSaleForm(token,
            ("Products[0].Sku", "MISSING"),
            ("Products[0].Quantity", "1")));
        var html = await response.Content.ReadAsStringAsync();
        var stock = await factory.Services.GetRequiredService<IInventoryRepository>()
            .GetByBranchAndSkuAsync("IND01", "ENV001");
        var transactions = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByBranchAsync("IND01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Select a product that exists in the catalogue", html);
        Assert.Contains("value=\"MISSING\"", html);
        Assert.Equal(5, stock!.Quantity);
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task CounterSaleRejectsInsufficientStockWithoutCreatingTransaction()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedCounterSaleDataAsync(stockQuantity: 1);
        using var client = await factory.CreateAuthenticatedClientAsync();
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Create");

        using var response = await client.PostAsync("/Transactions/Create", CreateCounterSaleForm(token,
            ("Products[0].Sku", "ENV001"),
            ("Products[0].Quantity", "2")));
        var stock = await factory.Services.GetRequiredService<IInventoryRepository>()
            .GetByBranchAndSkuAsync("IND01", "ENV001");
        var transactions = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByBranchAsync("IND01");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("not enough stock", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, stock!.Quantity);
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task CounterSaleRequiresAntiforgeryToken()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedCounterSaleDataAsync();
        using var client = await factory.CreateAuthenticatedClientAsync();

        using var response = await client.PostAsync("/Transactions/Create", CreateCounterSaleForm(null,
            ("Products[0].Sku", "ENV001"),
            ("Products[0].Quantity", "1")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuditorCannotOpenCounterSaleForm()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedCounterSaleDataAsync();
        using var client = await factory.CreateAuthenticatedClientAsync(StaffRole.Auditor);

        using var response = await client.GetAsync("/Transactions/Create");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task CounterSaleRejectsInactiveStaffAndClosedBranch()
    {
        using var inactiveFactory = new TestWebApplicationFactory();
        await inactiveFactory.SeedCounterSaleDataAsync();
        using var inactiveClient = await inactiveFactory.CreateAuthenticatedClientAsync();
        var inactiveToken = await GetAntiforgeryTokenAsync(inactiveClient, "/Transactions/Create");
        var inactiveStaff = await inactiveFactory.Services.GetRequiredService<IStaffRepository>()
            .GetByIdAsync("staff-test-1");
        inactiveStaff!.Deactivate();
        await inactiveFactory.Services.GetRequiredService<IStaffRepository>().UpdateAsync(inactiveStaff);
        using var inactiveResponse = await inactiveClient.PostAsync("/Transactions/Create", CreateCounterSaleForm(inactiveToken,
            ("Products[0].Sku", "ENV001"),
            ("Products[0].Quantity", "1")));
        var inactiveStock = await inactiveFactory.Services.GetRequiredService<IInventoryRepository>()
            .GetByBranchAndSkuAsync("IND01", "ENV001");

        Assert.Equal(HttpStatusCode.Forbidden, inactiveResponse.StatusCode);
        Assert.Equal(12, inactiveStock!.Quantity);

        using var closedFactory = new TestWebApplicationFactory();
        await closedFactory.SeedCounterSaleDataAsync();
        using var closedClient = await closedFactory.CreateAuthenticatedClientAsync();
        var closedToken = await GetAntiforgeryTokenAsync(closedClient, "/Transactions/Create");
        var branch = await closedFactory.Services.GetRequiredService<IBranchRepository>()
            .GetByCodeAsync(new BranchCode("IND01"));
        branch!.Deactivate();
        await closedFactory.Services.GetRequiredService<IBranchRepository>().UpdateAsync(branch);
        using var closedResponse = await closedClient.PostAsync("/Transactions/Create", CreateCounterSaleForm(closedToken,
            ("Products[0].Sku", "ENV001"),
            ("Products[0].Quantity", "1")));
        var closedStock = await closedFactory.Services.GetRequiredService<IInventoryRepository>()
            .GetByBranchAndSkuAsync("IND01", "ENV001");

        Assert.Equal(HttpStatusCode.Conflict, closedResponse.StatusCode);
        Assert.Equal(12, closedStock!.Quantity);
    }

    private static FormUrlEncodedContent CreateCounterSaleForm(
        string? token,
        params (string Key, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Key, item => item.Value);
        if (token is not null)
        {
            fields["__RequestVerificationToken"] = token;
        }

        fields["CounterId"] = "C001";
        fields["PaymentMethod"] = "Cash";
        return new FormUrlEncodedContent(fields);
    }

    [Fact]
    public async Task CounterClerkCannotVoidTransaction()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedTransactionAsync("TXN-CLERK-1");
        using var client = await factory.CreateAuthenticatedClientAsync(StaffRole.CounterClerk);
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");

        using var response = await PostVoidAsync(client, token, "TXN-CLERK-1", "Not permitted");
        var saved = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByIdAsync("TXN-CLERK-1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TransactionStatus.Paid, saved!.Status);
    }

    [Fact]
    public async Task BranchManagerCannotVoidTransactionFromAnotherBranch()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedTransactionAsync("TXN-WRONG-BRANCH-1", "IND02");
        using var client = await factory.CreateAuthenticatedClientAsync(
            StaffRole.BranchManager,
            branchCode: "IND01");
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");

        using var response = await PostVoidAsync(client, token, "TXN-WRONG-BRANCH-1", "Wrong branch");
        var saved = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByIdAsync("TXN-WRONG-BRANCH-1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TransactionStatus.Paid, saved!.Status);
    }

    [Fact]
    public async Task MissingVoidReasonIsRejectedWithoutMutatingTransaction()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedTransactionAsync("TXN-NO-REASON-1");
        using var client = await factory.CreateAuthenticatedClientAsync(StaffRole.BranchManager);
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Details/TXN-NO-REASON-1");

        using var response = await PostVoidAsync(client, token, "TXN-NO-REASON-1", "");
        var page = await response.Content.ReadAsStringAsync();
        var saved = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByIdAsync("TXN-NO-REASON-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Enter a reason for voiding", page);
        Assert.Equal(TransactionStatus.Paid, saved!.Status);
    }

    [Fact]
    public async Task OpenTransactionCannotBeVoidedAndRemainsUnchanged()
    {
        using var factory = new TestWebApplicationFactory();
        await factory.SeedTransactionAsync("TXN-OPEN-1", status: TransactionStatus.Open);
        using var client = await factory.CreateAuthenticatedClientAsync(StaffRole.BranchManager);
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");

        using var response = await PostVoidAsync(client, token, "TXN-OPEN-1", "Invalid state");
        var page = await response.Content.ReadAsStringAsync();
        var saved = await factory.Services.GetRequiredService<ICounterTransactionRepository>()
            .GetByIdAsync("TXN-OPEN-1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Only paid transactions can be voided", page);
        Assert.Equal(TransactionStatus.Open, saved!.Status);
        Assert.Null(saved.Reason);
    }

    private static HttpClient CreateCourierClient(TestWebApplicationFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        return client;
    }

    private static async Task<HttpResponseMessage> PostCourierStatusAsync(
        HttpClient client,
        string shipmentId,
        string status,
        string? apiKey = TestWebApplicationFactory.TestApiKey,
        string? origin = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/integrations/courier/shipments/{shipmentId}/status")
        {
            Content = JsonContent.Create(new CourierStatusUpdateRequest { Status = status })
        };

        if (apiKey is not null)
        {
            request.Headers.Add("X-Api-Key", apiKey);
        }

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PostVoidAsync(
        HttpClient client,
        string token,
        string transactionId,
        string reason) => client.PostAsync(
            "/Transactions/Void",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["TransactionId"] = transactionId,
                ["VoidReason"] = reason,
                ["__RequestVerificationToken"] = token
            }));

    private static Task<HttpResponseMessage> PostRefundAsync(
        HttpClient client,
        string token,
        string transactionId,
        string reason) => client.PostAsync(
            "/Transactions/Refund",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["TransactionId"] = transactionId,
                ["RefundReason"] = reason,
                ["__RequestVerificationToken"] = token
            }));

    [Fact]
    public void HostUsesTheConfiguredJsonDataDirectoryAndInquiryService()
    {
        using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var options = factory.Services.GetRequiredService<JsonPersistenceOptions>();

        Assert.Equal(Path.GetFullPath(factory.DataDirectory), options.DataDirectory);
        Assert.IsType<JsonBranchRepository>(services.GetRequiredService<IBranchRepository>());
        Assert.IsType<JsonProductRepository>(services.GetRequiredService<IProductRepository>());
        Assert.IsType<JsonInventoryRepository>(services.GetRequiredService<IInventoryRepository>());
        Assert.IsType<JsonStaffCredentialRepository>(services.GetRequiredService<IStaffCredentialRepository>());
        Assert.IsType<Pbkdf2StaffPasswordHasher>(services.GetRequiredService<IStaffPasswordHasher>());
        Assert.NotNull(services.GetRequiredService<InventoryInquiryService>());
    }

    private static Task<HttpResponseMessage> SubmitInquiryAsync(
        HttpClient client,
        string branchCode,
        string sku) => SubmitWithTokenAsync(client, branchCode, sku);

    private static async Task<HttpResponseMessage> SubmitWithTokenAsync(
        HttpClient client,
        string branchCode,
        string sku)
    {
        using var form = await CreateInquiryFormAsync(client, branchCode, sku);
        return await client.PostAsync("/Inventory/Search", form);
    }

    private static async Task<FormUrlEncodedContent> CreateInquiryFormAsync(
        HttpClient client,
        string branchCode,
        string sku)
    {
        using var page = await client.GetAsync("/Inventory/Index");
        var html = await page.Content.ReadAsStringAsync();
        var token = ExtractAntiforgeryToken(html);

        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["BranchCode"] = branchCode,
            ["Sku"] = sku,
            ["__RequestVerificationToken"] = token
        });
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ExtractAntiforgeryToken(await response.Content.ReadAsStringAsync());
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var tokenMatch = Regex.Match(
            html,
            "name=\\\"__RequestVerificationToken\\\" type=\\\"hidden\\\" value=\\\"(?<token>[^\\\"]+)\\\"",
            RegexOptions.CultureInvariant);

        Assert.True(tokenMatch.Success, "The form should render an antiforgery token.");
        return WebUtility.HtmlDecode(tokenMatch.Groups["token"].Value);
    }

    private static Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string token,
        string email,
        string password) => client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password,
                ["__RequestVerificationToken"] = token
            }));

    private sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        public const string TestEmail = "clerk@example.test";
        public const string TestPassword = "Test-secret-42!";
        public const string TestApiKey = "test-only-integration-key-91";

        private readonly string _environment;
        private readonly bool _maintenanceEnabled;
        private readonly bool _configureIntegrationKey;
        private readonly string[] _allowedIntegrationIps;
        private readonly bool _configurePartnerOrigin;

        public string DataDirectory { get; } = Directory
            .CreateTempSubdirectory("ApexPostWebIntegrationTests")
            .FullName;

        public TestWebApplicationFactory(
            string environment = "Development",
            bool maintenanceEnabled = false,
            bool configureIntegrationKey = true,
            string[]? allowedIntegrationIps = null,
            bool configurePartnerOrigin = true)
        {
            _environment = environment;
            _maintenanceEnabled = maintenanceEnabled;
            _configureIntegrationKey = configureIntegrationKey;
            _allowedIntegrationIps = allowedIntegrationIps ?? ["192.0.2.19"];
            _configurePartnerOrigin = configurePartnerOrigin;
        }

        public async Task<HttpClient> CreateAuthenticatedClientAsync(
            StaffRole role = StaffRole.CounterClerk,
            string branchCode = "IND01",
            WebApplicationFactoryClientOptions? options = null)
        {
            await SeedStaffAsync(role, branchCode);

            var clientOptions = options ?? new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            };
            clientOptions.BaseAddress = new Uri("https://localhost");
            var client = CreateClient(clientOptions);
            var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");
            using var login = await PostLoginAsync(client, token, TestEmail, TestPassword);

            if (login.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Redirect))
            {
                client.Dispose();
                throw new InvalidOperationException("Test staff login did not complete successfully.");
            }

            return client;
        }

        public async Task SeedStaffAsync(
            StaffRole role = StaffRole.CounterClerk,
            string branchCode = "IND01")
        {
            var staffRepository = Services.GetRequiredService<IStaffRepository>();
            if (await staffRepository.GetByIdAsync("staff-test-1") is not null)
            {
                return;
            }

            await staffRepository.AddAsync(new Staff(
                "staff-test-1",
                "Test Clerk",
                TestEmail,
                role,
                new BranchCode(branchCode)));

            var hasher = Services.GetRequiredService<IStaffPasswordHasher>();
            var credentialStore = new JsonFileStore<StaffCredential>(
                Path.Combine(DataDirectory, "staff-credentials.json"),
                credential => credential.StaffId);
            await credentialStore.AddAsync(new StaffCredential(
                "staff-test-1",
                hasher.HashPassword(TestPassword)));
        }

        public async Task SeedInventoryAsync(bool includeStock = true)
        {
            var branch = new Branch(
                new BranchCode("IND01"),
                "Central Branch",
                "South",
                new TimeOnly(9, 0),
                new TimeOnly(17, 0));
            var product = new Product(
                new Sku("ENV001"),
                "Envelope",
                ProductCategory.Packaging,
                new Money(50m),
                TaxCategory.Standard);

            await Services.GetRequiredService<IBranchRepository>()
                .AddAsync(branch);

            var products = new JsonFileStore<Product>(
                Path.Combine(DataDirectory, "products.json"),
                item => item.Sku.Value);
            await products.AddAsync(product);

            if (includeStock)
            {
                await Services.GetRequiredService<IInventoryRepository>()
                    .AddAsync(new InventoryStock(
                        new BranchCode("IND01"),
                        new Sku("ENV001"),
                        12));
            }
        }

        public async Task SeedShipmentAsync(string shipmentId)
        {
            await Services.GetRequiredService<IShipmentRepository>().AddAsync(new Shipment(
                shipmentId,
                "Parcel",
                new Weight(1.25m),
                20m,
                15m,
                10m,
                "ZONE1",
                ShippingServiceLevel.Standard));
        }

        public async Task SeedTransactionAsync(
            string transactionId,
            string branchCode = "IND01",
            TransactionStatus status = TransactionStatus.Paid)
        {
            var transaction = new CounterTransaction(
                transactionId,
                new BranchCode(branchCode),
                "COUNTER01",
                "staff-test-1");
            if (status is TransactionStatus.Paid or TransactionStatus.Voided or TransactionStatus.Refunded)
            {
                transaction.AddLineItem(new TransactionLineItem(
                    "Envelope",
                    "Retail",
                    1,
                    new Money(50m)));
                transaction.CompletePayment(PaymentMethod.Cash);
                if (status == TransactionStatus.Voided)
                {
                    transaction.Void("Already voided");
                }
                else if (status == TransactionStatus.Refunded)
                {
                    transaction.Refund("Already refunded");
                }
            }

            await Services.GetRequiredService<ICounterTransactionRepository>().AddAsync(transaction);
        }

        public async Task SeedCounterSaleDataAsync(int stockQuantity = 12)
        {
            await Services.GetRequiredService<IBranchRepository>().AddAsync(new Branch(
                new BranchCode("IND01"),
                "Central Branch",
                "South",
                new TimeOnly(0, 0),
                new TimeOnly(23, 59)));
            await Services.GetRequiredService<ICounterRepository>().AddAsync(new Counter(
                "C001",
                new BranchCode("IND01"),
                "Central Branch counter"));
            var products = new JsonFileStore<Product>(
                Path.Combine(DataDirectory, "products.json"),
                product => product.Sku.Value);
            await products.AddAsync(new Product(
                new Sku("ENV001"),
                "Envelope",
                ProductCategory.Packaging,
                new Money(50m),
                TaxCategory.Standard));
            await Services.GetRequiredService<IInventoryRepository>().AddAsync(new InventoryStock(
                new BranchCode("IND01"),
                new Sku("ENV001"),
                stockQuantity));
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Maintenance:Enabled"] = _maintenanceEnabled.ToString(),
                };
                if (_configurePartnerOrigin)
                {
                    settings["Integration:AllowedOrigin"] = "https://courier.apexpost.test";
                }

                for (var index = 0; index < _allowedIntegrationIps.Length; index++)
                {
                    settings[$"Integration:AllowedIps:{index}"] = _allowedIntegrationIps[index];
                }
                if (_configureIntegrationKey)
                {
                    settings["Integration:ApiKey"] = TestApiKey;
                }

                configuration.AddInMemoryCollection(settings);
            });
            builder.ConfigureServices(services =>
            {
                services.AddControllersWithViews()
                    .AddApplicationPart(typeof(TestProbeController).Assembly);
                services.AddSingleton<IStartupFilter>(new TestRemoteAddressStartupFilter());
                services.RemoveAll<JsonPersistenceOptions>();
                services.RemoveAll<JsonAuditLog>();
                services.RemoveAll<IInventoryRepository>();
                services.RemoveAll<ICounterTransactionRepository>();
                services.RemoveAll<IShipmentRepository>();
                services.RemoveAll<IMoneyOrderRepository>();

                services.AddInfrastructure(options => options.DataDirectory = DataDirectory);
                services.AddDataProtection()
                    .PersistKeysToFileSystem(
                        new DirectoryInfo(Path.Combine(DataDirectory, "keys")));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && Directory.Exists(DataDirectory))
            {
                Directory.Delete(DataDirectory, recursive: true);
            }
        }
    }
}

internal sealed class TestRemoteAddressStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, continuation) =>
        {
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
            await continuation();
        });
        next(app);
    };
}

[ApiController]
[Route("test-probes")]
[AllowAnonymous]
public sealed class TestProbeController : ControllerBase
{
    [HttpGet("api-key")]
    [ApiKeyProtected]
    [IpAllowlistProtected("Integration:AllowedIps")]
    public IActionResult ApiKeyProtectedProbe() => Ok(new { status = "accepted" });

    [HttpGet("throw")]
    public IActionResult ThrowForExceptionHandlingProbe() =>
        throw new InvalidOperationException("private-diagnostic-detail");
}
