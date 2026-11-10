using System;
using System.Net.Http.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ApexPost.Courier.Console.Services;
using ApexPost.Courier.Console.Handlers;
using ApexPost.Courier.Console.UI;
using ApexPost.Courier.Offline.Persistence;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Api.DTOs;

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
        builder.Services.Configure<ApexApiClientOptions>(options =>
        {
            options.BaseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:54402";
        });

        // Services
        builder.Services.AddSingleton<ITokenStorage, FileTokenStorage>();
        builder.Services.AddSingleton<AuthHeaderHandler>();

        // Typed HTTP client with Polly resilience
        builder.Services.AddHttpClient<IApexApiClient, ApexApiClient>(client =>
        {
            var baseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:54402";
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<AuthHeaderHandler>()
        .AddStandardResilienceHandler(options =>
        {
            // Retry policy: 3 attempts with exponential backoff and jitter
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

            // Circuit breaker
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

            // Timeout
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        });

        // Sync service
        builder.Services.AddSingleton<ISyncService, SyncService>();

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