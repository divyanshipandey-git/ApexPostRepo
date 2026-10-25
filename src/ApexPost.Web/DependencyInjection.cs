using System.Threading.RateLimiting;
using System.Globalization;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using ApexPost.Web.Filters;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Shipping;
using ApexPost.Application.Services.MoneyOrders;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Cryptography;
using System.Text;

namespace ApexPost.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddCounterSaleConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!decimal.TryParse(
                configuration["MoneyOrder:Fee"],
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var moneyOrderFee))
        {
            moneyOrderFee = 10m;
        }

        if (!decimal.TryParse(
                configuration["MoneyOrder:VerificationThreshold"],
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var verificationThreshold))
        {
            verificationThreshold = 10_000m;
        }

        var moneyOrderSettings = new MoneyOrderSettings(
            new Money(moneyOrderFee),
            new Money(verificationThreshold));
        services.RemoveAll<MoneyOrderSettings>();
        services.RemoveAll<MoneyOrderPolicy>();
        services.TryAddSingleton(moneyOrderSettings);
        services.TryAddSingleton(new MoneyOrderPolicy(moneyOrderSettings.VerificationThreshold));

        var shippingRules = new List<ShippingPricingRule>();
        foreach (var section in configuration.GetSection("Shipping:PricingRules").GetChildren())
        {
            if (!Enum.TryParse<ShippingServiceLevel>(section["ServiceLevel"], out var level) ||
                !Enum.IsDefined(level) ||
                !decimal.TryParse(section["PricePerKilogram"], NumberStyles.Number, CultureInfo.InvariantCulture, out var perKilogram) ||
                !decimal.TryParse(section["BasePrice"], NumberStyles.Number, CultureInfo.InvariantCulture, out var basePrice) ||
                string.IsNullOrWhiteSpace(section["DestinationZone"]))
            {
                throw new InvalidOperationException("A configured shipping pricing rule is invalid.");
            }

            shippingRules.Add(new ShippingPricingRule(
                section["DestinationZone"]!,
                level,
                perKilogram,
                new Money(basePrice)));
        }

        var maximumWeight = configuration.GetValue<decimal?>("Shipping:MaximumWeightKg") ?? 30m;
        if (maximumWeight <= 0)
        {
            throw new InvalidOperationException("The configured maximum shipping weight must be positive.");
        }

        services.RemoveAll<ShippingService>();
        services.AddScoped<ShippingService>(provider => new ShippingService(
            provider.GetRequiredService<IShipmentRepository>(),
            shippingRules,
            maximumWeight,
            provider.GetRequiredService<TimeProvider>()));

        var courierTimeoutSeconds = configuration.GetValue<int?>("Shipping:CourierTrackingTimeoutSeconds") ?? 2;
        var simulatedTrackingDelayMilliseconds = configuration.GetValue<int?>("Shipping:SimulatedTrackingDelayMilliseconds") ?? 0;
        if (courierTimeoutSeconds is < 1 or > 60 || simulatedTrackingDelayMilliseconds is < 0 or > 60_000)
        {
            throw new InvalidOperationException("Courier tracking timeout or simulation delay is outside its supported range.");
        }

        services.RemoveAll<CourierStatusService>();
        services.AddScoped(provider => new CourierStatusService(
            provider.GetRequiredService<IShipmentRepository>(),
            provider.GetRequiredService<TimeProvider>(),
            TimeSpan.FromSeconds(courierTimeoutSeconds),
            TimeSpan.FromMilliseconds(simulatedTrackingDelayMilliseconds)));

        foreach (var section in configuration.GetSection("Tax:Rates").GetChildren())
        {
            if (!Enum.TryParse<TaxCategory>(section["Category"], out var category) ||
                !Enum.IsDefined(category) ||
                !decimal.TryParse(section["Rate"], NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
            {
                throw new InvalidOperationException("A configured tax rate is invalid.");
            }

            services.AddSingleton(new TaxRate(category, rate));
        }

        return services;
    }

    public static IServiceCollection AddWebSecurity(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "__Host-ApexPost.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.BranchManagerOnly, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole(nameof(StaffRole.BranchManager)));

            options.AddPolicy(AuthorizationPolicies.SameBranchOnly, policy =>
                policy.RequireAuthenticatedUser()
                    .AddRequirements(new SameBranchRequirement()));

            options.AddPolicy(AuthorizationPolicies.InventoryStaff, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole(
                        nameof(StaffRole.CounterClerk),
                        nameof(StaffRole.BranchManager),
                        nameof(StaffRole.Administrator))
                    .AddRequirements(new SameBranchRequirement()));

            options.AddPolicy(AuthorizationPolicies.CounterSales, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole(
                        nameof(StaffRole.CounterClerk),
                        nameof(StaffRole.BranchManager),
                        nameof(StaffRole.Administrator)));
        });

        services.AddSingleton<IAuthorizationHandler, SameBranchAuthorizationHandler>();
        services.AddScoped<AuditActionFilter>();
        services.AddScoped<ValidationActionFilter>();
        services.AddScoped<PerformanceActionFilter>();
        services.Configure<PerformanceFilterOptions>(options =>
        {
            options.WarningThresholdMilliseconds = configuration?.GetValue<int?>(
                "Observability:MVCSlowActionWarningMilliseconds") ?? 500;
            if (options.WarningThresholdMilliseconds <= 0)
            {
                throw new InvalidOperationException("The MVC slow-action warning threshold must be positive.");
            }
        });
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.AddService<AuditActionFilter>();
            options.Filters.AddService<ValidationActionFilter>();
            options.Filters.AddService<PerformanceActionFilter>();
        });

        services.AddCors(options => options.AddPolicy("PartnerIntegration", policy =>
        {
            var partnerOrigin = configuration?["Integration:AllowedOrigin"];
            if (Uri.TryCreate(partnerOrigin, UriKind.Absolute, out var origin) &&
                origin.Scheme is "https" or "http" &&
                string.IsNullOrEmpty(origin.PathAndQuery.TrimEnd('/')))
            {
                policy.WithOrigins(origin.GetLeftPart(UriPartial.Authority));
            }
            else
            {
                policy.SetIsOriginAllowed(static _ => false);
            }

            policy.WithMethods("GET", "POST")
                .WithHeaders("Content-Type", "X-Api-Key")
                .WithExposedHeaders("X-Correlation-Id");
        }));
        services.AddHealthChecks();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            var configuredPartnerKey = configuration?["Integration:ApiKey"];
            options.AddPolicy(AuthorizationPolicies.LoginRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.AddPolicy(AuthorizationPolicies.PartnerIntegrationRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartnerPartitionKey(context, configuredPartnerKey),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    private static string GetPartnerPartitionKey(HttpContext context, string? configuredPartnerKey)
    {
        var suppliedKey = context.Request.Headers["X-Api-Key"].ToString();
        if (!string.IsNullOrWhiteSpace(configuredPartnerKey) && !string.IsNullOrEmpty(suppliedKey))
        {
            var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredPartnerKey));
            var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedKey));
            var isConfiguredPartner = CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
            CryptographicOperations.ZeroMemory(expectedHash);
            CryptographicOperations.ZeroMemory(suppliedHash);
            if (isConfiguredPartner)
            {
                return "configured-partner";
            }
        }

        return $"untrusted:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
