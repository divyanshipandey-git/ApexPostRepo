using System;
using Polly.CircuitBreaker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ApexPost.Courier.Console.Services;
using ApexPost.Courier.Console.Handlers;
using ApexPost.Courier.Console.UI;
using ApexPost.Courier.Offline.Persistence;
using ApexPost.Courier.Offline.Abstractions;

namespace ApexPost.Courier.Console;

internal class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Configuration
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        // Logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // Options
        builder.Services.Configure<CourierOptions>(builder.Configuration.GetSection("Courier"));
        builder.Services.Configure<TokenStorageOptions>(builder.Configuration.GetSection("TokenStorage"));
        builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection("Courier"));
        builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));
        builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection("Sync"));
        builder.Services.AddSingleton<IApiEndpointResolver, ApiEndpointResolver>();
        builder.Services.AddSingleton<IResilienceStateTracker, ResilienceStateTracker>();

        // Token storage: DPAPI-encrypted file (console equivalent of MAUI SecureStorage).
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Token storage uses Windows DPAPI. Register a SecureStorage-backed ITokenStorage for other platforms.");
        }
        builder.Services.AddSingleton<ITokenStorage, FileTokenStorage>();
        builder.Services.AddSingleton<AuthHeaderHandler>();

        // Typed HTTP client with Polly resilience. The base address depends on the platform
        // (Windows host vs Android emulator) and comes from IApiEndpointResolver.
        builder.Services.AddHttpClient<IApexApiClient, ApexApiClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IApiEndpointResolver>().BaseAddress;
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<AuthHeaderHandler>()
        .AddStandardResilienceHandler();
        builder.Services.AddOptions<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions>("IApexApiClient-standard")
            .Configure<IServiceProvider>((options, sp) =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ApexPost.Resilience");
            var tracker = sp.GetRequiredService<IResilienceStateTracker>();

            // Retry policy: 3 retries with exponential backoff and jitter. Every retry is logged.
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            options.Retry.ShouldHandle = args =>
            {
                if (args.Outcome.Exception is HttpRequestException)
                    return ValueTask.FromResult(true);

                if (args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                    (int)(args.Outcome.Result?.StatusCode ?? 0) >= 500)
                    return ValueTask.FromResult(true);

                return ValueTask.FromResult(false);
            };
            options.Retry.OnRetry = args =>
            {
                logger.LogWarning(
                    "Retry {Attempt}/{Max} in {Delay:0.0}s after {Reason}",
                    args.AttemptNumber + 1,
                    options.Retry.MaxRetryAttempts,
                    args.RetryDelay.TotalSeconds,
                    args.Outcome.Exception?.GetType().Name ?? $"HTTP {(int?)args.Outcome.Result?.StatusCode}");
                return default;
            };

            // Circuit breaker: every state change is logged and forwarded to the sync service / UI.
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.OnOpened = args =>
            {
                tracker.CircuitOpened(
                    args.BreakDuration,
                    args.Outcome.Exception?.GetType().Name ?? $"HTTP {(int?)args.Outcome.Result?.StatusCode}");
                return default;
            };
            options.CircuitBreaker.OnHalfOpened = _ =>
            {
                tracker.CircuitHalfOpened();
                return default;
            };
            options.CircuitBreaker.OnClosed = _ =>
            {
                tracker.CircuitClosed();
                return default;
            };

            // Timeouts
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        });

        // Sync service
        builder.Services.AddSingleton<ISyncService, SyncService>();

        // Real connectivity events (NetworkChange) drive ISyncService.OnConnectivityChangedAsync.
        builder.Services.AddHostedService<NetworkConnectivityMonitor>();

        // Courier database (wraps Part A Couchbase Lite)
        builder.Services.AddSingleton<CourierDatabase>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetService<ILogger<CourierDatabase>>();
            var dbPath = config["Courier:DatabasePath"];
            return new CourierDatabase(dbPath, logger);
        });
        builder.Services.AddSingleton<CouchbaseScanRepository>();
        builder.Services.AddSingleton<IScanRepository>(sp => sp.GetRequiredService<CouchbaseScanRepository>());
        builder.Services.AddSingleton<ICourierDatabase, CourierDatabaseWrapper>();

        // Console UI
        builder.Services.AddSingleton<ConsoleMenu>();

        // Build and run
        var host = builder.Build();

        try
        {
            await host.StartAsync(); // starts the connectivity monitor
            var menu = host.Services.GetRequiredService<ConsoleMenu>();
            await menu.RunAsync();
        }
        catch (Exception ex)
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogCritical(ex, "Application terminated unexpectedly");
        }
        finally
        {
            await host.StopAsync();
        }
    }
}