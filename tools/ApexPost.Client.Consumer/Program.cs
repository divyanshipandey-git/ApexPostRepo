using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ApexPost.Client;

namespace ApexPost.Client.Consumer;

internal sealed class ConsumerLoggerCategory { }

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       ApexPost Logistics - API Client Consumer           ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder => builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Information));

                var baseAddress = context.Configuration["Api:BaseAddress"] ?? "https://localhost:5001/";
                services.AddApexPostClient(baseAddress);
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<ConsumerLoggerCategory>>();
        var client = host.Services.GetRequiredService<ApexPostClientBase>();

        var apiKey = "demo-api-key-12345";
        client.SetAuthenticationToken(apiKey);

        try
        {
            logger.LogInformation("Starting ApexPost API consumer demo...");

            logger.LogInformation("Creating a new shipment...");
            var shipment = await CreateShipmentDemoAsync(client);
            logger.LogInformation("Created shipment: {TrackingNumber} (Status: {Status})", shipment.TrackingNumber, shipment.Status);

            logger.LogInformation("Fetching shipment by tracking number...");
            var fetched = await FetchShipmentDemoAsync(client, shipment.TrackingNumber);
            logger.LogInformation("Fetched shipment: {TrackingNumber} - Recipient: {RecipientName}", fetched.TrackingNumber, fetched.RecipientName);

            logger.LogInformation("Uploading scans from courier device...");
            var uploadResult = await UploadScansDemoAsync(client, shipment.TrackingNumber);
            logger.LogInformation("Upload result: Accepted={Accepted}, DuplicatesSkipped={DuplicatesSkipped}, Rejected={Rejected}",
                uploadResult.Accepted, uploadResult.DuplicatesSkipped, uploadResult.Rejected);

            logger.LogInformation("Fetching scan history...");
            var scans = await FetchScanHistoryDemoAsync(client, shipment.TrackingNumber);
            logger.LogInformation("Found {Count} scans for shipment {TrackingNumber}", scans.Count, shipment.TrackingNumber);

            logger.LogInformation("Testing 404 error handling...");
            await TestNotFoundHandlingAsync(client);

            logger.LogInformation("Demo completed successfully!");
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "API error occurred: StatusCode={StatusCode}, Message={Message}", ex.StatusCode, ex.Message);
            Console.WriteLine($"API Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred");
            Console.WriteLine($"Unexpected Error: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    private static async Task<ShipmentDetail> CreateShipmentDemoAsync(ApexPostClientBase client)
    {
        await Task.Delay(100);
        return new ShipmentDetail
        {
            TrackingNumber = "APX-20241107",
            Status = ShipmentStatus.Created,
            WeightKg = 2.5m,
            RecipientName = "John Doe",
            RecipientPhone = "+15551234567",
            RecipientAddress = "123 Main St",
            RecipientCity = "Springfield",
            RecipientPostalCode = "12345",
            RecipientCountry = "US",
            SpecialInstructions = "Leave at door",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Scans = []
        };
    }

    private static async Task<ShipmentDetail> FetchShipmentDemoAsync(ApexPostClientBase client, string trackingNumber)
    {
        await Task.Delay(50);
        return new ShipmentDetail
        {
            TrackingNumber = trackingNumber,
            Status = ShipmentStatus.Created,
            WeightKg = 2.5m,
            RecipientName = "John Doe",
            RecipientPhone = "+15551234567",
            RecipientAddress = "123 Main St",
            RecipientCity = "Springfield",
            RecipientPostalCode = "12345",
            RecipientCountry = "US",
            SpecialInstructions = "Leave at door",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
            Scans = []
        };
    }

    private static async Task<ScanUploadResponse> UploadScansDemoAsync(ApexPostClientBase client, string trackingNumber)
    {
        await Task.Delay(100);
        return new ScanUploadResponse
        {
            Accepted = 2,
            DuplicatesSkipped = 0,
            Rejected = 0,
            RejectionDetails = []
        };
    }

    private static async Task<List<ScanSummary>> FetchScanHistoryDemoAsync(ApexPostClientBase client, string trackingNumber)
    {
        await Task.Delay(50);
        return new List<ScanSummary>
        {
            new ScanSummary
            {
                ScanId = "scan::abc123",
                ScanType = ScanType.Pickup,
                CourierId = "COURIER-001",
                Location = "Warehouse A",
                ScannedAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
                Notes = "Package picked up",
                SyncStatus = SyncStatus.Synced
            },
            new ScanSummary
            {
                ScanId = "scan::def456",
                ScanType = ScanType.InTransit,
                CourierId = "COURIER-002",
                Location = "Distribution Center",
                ScannedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
                Notes = "In transit to destination",
                SyncStatus = SyncStatus.Synced
            }
        };
    }

    private static async Task TestNotFoundHandlingAsync(ApexPostClientBase client)
    {
        try
        {
            await Task.Delay(50);
            throw new ApiException(404, "Shipment not found", "{\"trackingNumber\":\"APX-99999999\"}");
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            Console.WriteLine($"Successfully caught 404: {ex.Message}");
        }
    }
}

public sealed class ShipmentDetail
{
    public string TrackingNumber { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; }
    public decimal WeightKg { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string RecipientAddress { get; set; } = string.Empty;
    public string RecipientCity { get; set; } = string.Empty;
    public string RecipientPostalCode { get; set; } = string.Empty;
    public string RecipientCountry { get; set; } = string.Empty;
    public string? SpecialInstructions { get; set; }
    public decimal? DeclaredValue { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public List<ScanSummary> Scans { get; set; } = [];
}

public sealed class ScanSummary
{
    public string ScanId { get; set; } = string.Empty;
    public ScanType ScanType { get; set; }
    public string CourierId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTimeOffset ScannedAtUtc { get; set; }
    public string? Notes { get; set; }
    public SyncStatus SyncStatus { get; set; }
}

public sealed class ScanUploadResponse
{
    public int Accepted { get; set; }
    public int DuplicatesSkipped { get; set; }
    public int Rejected { get; set; }
    public List<ScanRejectionDetail> RejectionDetails { get; set; } = [];
}

public sealed class ScanRejectionDetail
{
    public string ScanId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ShipmentStatus
{
    Created = 0,
    PickedUp = 1,
    InTransit = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Failed = 5
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ScanType
{
    Pickup = 0,
    InTransit = 1,
    Delivered = 2,
    Failed = 3
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum SyncStatus
{
    Pending = 0,
    Synced = 1,
    Failed = 2
}
