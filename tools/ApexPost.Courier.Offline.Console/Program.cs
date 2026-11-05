using System.Diagnostics;
using Couchbase.Lite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Persistence;
using ApexPost.Courier.Offline.Services;

// Setup DI
var services = new ServiceCollection();
services.AddLogging(builder => builder
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information));

var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<CourierDatabase>>();

// Initialize database
var database = new CourierDatabase(logger: logger);
var scanRepository = new CouchbaseScanRepository(database);
var outboxService = new OutboxService(scanRepository, new FakeShipmentUploader(0.1));

ListenerToken? changeListenerToken = null;
var pendingCount = 0;

void OnDocumentsChanged(IEnumerable<string> changedIds)
{
    foreach (var id in changedIds)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n[Listener] Document changed: {id}");
        Console.ResetColor();
    }

    // Update pending count
    _ = Task.Run(async () =>
    {
        var pending = await scanRepository.GetBySyncStatusAsync(SyncStatus.Pending);
        pendingCount = pending.Count;
    });
}

// Register change listener
changeListenerToken = database.AddChangeListener(OnDocumentsChanged);

// Initialize pending count
var pending = await scanRepository.GetBySyncStatusAsync(SyncStatus.Pending);
pendingCount = pending.Count;

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║       ApexPost Courier Offline - Console Harness        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

var running = true;
while (running)
{
    PrintMenu();
    var input = Console.ReadLine()?.Trim();

    try
    {
        running = await HandleInputAsync(input!);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
    }
}

void PrintMenu()
{
    Console.WriteLine();
    Console.WriteLine($"Pending scans: {pendingCount}");
    Console.WriteLine("────────────────────────────────────────");
    Console.WriteLine("1. Add Scan");
    Console.WriteLine("2. Find by Tracking Number");
    Console.WriteLine("3. List Pending");
    Console.WriteLine("4. Simulate Sync");
    Console.WriteLine("5. Delete Scan");
    Console.WriteLine("6. Exit");
    Console.Write("Select option: ");
}

async Task<bool> HandleInputAsync(string input)
{
    if (input == "1") return await AddScanAsync();
    if (input == "2") return await FindByTrackingNumberAsync();
    if (input == "3") return await ListPendingAsync();
    if (input == "4") return await SimulateSyncAsync();
    if (input == "5") return await DeleteScanAsync();
    if (input == "6") return false;
    
    Console.WriteLine("Invalid option.");
    return true;
}

async Task<bool> AddScanAsync()
{
    Console.WriteLine();
    Console.Write("Tracking Number: ");
    var trackingNumber = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(trackingNumber)) { Console.WriteLine("Tracking number required."); return true; }

    Console.Write("Scan Type (0=Pickup, 1=InTransit, 2=Delivered, 3=Failed): ");
    if (!Enum.TryParse<ScanType>(Console.ReadLine(), out var scanType)) { Console.WriteLine("Invalid scan type."); return true; }

    Console.Write("Courier ID: ");
    var courierId = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(courierId)) { Console.WriteLine("Courier ID required."); return true; }

    Console.Write("Location: ");
    var location = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(location)) { Console.WriteLine("Location required."); return true; }

    Console.Write("Notes (optional): ");
    var notes = Console.ReadLine()?.Trim();

    var scan = ShipmentScan.Create(trackingNumber!, scanType, courierId!, location!, DateTimeOffset.UtcNow, notes);
    await scanRepository.CreateAsync(scan);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Scan created with ID: {scan.Id}");
    Console.ResetColor();

    return true;
}

async Task<bool> FindByTrackingNumberAsync()
{
    Console.WriteLine();
    Console.Write("Tracking Number: ");
    var trackingNumber = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(trackingNumber)) { Console.WriteLine("Tracking number required."); return true; }

    var scans = await scanRepository.GetByTrackingNumberAsync(trackingNumber!);
    
    if (scans.Count == 0)
    {
        Console.WriteLine("No scans found for this tracking number.");
    }
    else
    {
        Console.WriteLine($"\nFound {scans.Count} scan(s):");
        foreach (var scan in scans)
        {
            PrintScan(scan);
        }
    }

    return true;
}

async Task<bool> ListPendingAsync()
{
    Console.WriteLine();
    var scans = await scanRepository.GetBySyncStatusAsync(SyncStatus.Pending);
    pendingCount = scans.Count;

    if (scans.Count == 0)
    {
        Console.WriteLine("No pending scans.");
    }
    else
    {
        Console.WriteLine($"Pending scans ({scans.Count}):");
        foreach (var scan in scans)
        {
            PrintScan(scan);
        }
    }

    return true;
}

async Task<bool> SimulateSyncAsync()
{
    Console.WriteLine();
    Console.WriteLine("Processing pending scans...");
    
    var processed = await outboxService.ProcessPendingAsync();
    
    pending = await scanRepository.GetBySyncStatusAsync(SyncStatus.Pending);
    pendingCount = pending.Count;

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Sync complete. Processed: {processed}, Remaining pending: {pendingCount}");
    Console.ResetColor();

    return true;
}

async Task<bool> DeleteScanAsync()
{
    Console.WriteLine();
    Console.Write("Scan ID to delete: ");
    var id = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(id)) { Console.WriteLine("Scan ID required."); return true; }

    await scanRepository.DeleteAsync(id!);
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"Scan {id} deleted.");
    Console.ResetColor();

    return true;
}

void PrintScan(ShipmentScan scan)
{
    Console.WriteLine($"  ID: {scan.Id}");
    Console.WriteLine($"  Tracking: {scan.TrackingNumber}");
    Console.WriteLine($"  Type: {scan.ScanType}");
    Console.WriteLine($"  Courier: {scan.CourierId}");
    Console.WriteLine($"  Location: {scan.Location}");
    Console.WriteLine($"  Time: {scan.ScannedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
    Console.WriteLine($"  Status: {scan.SyncStatus}");
    if (!string.IsNullOrEmpty(scan.Notes))
    {
        Console.WriteLine($"  Notes: {scan.Notes}");
    }
    Console.WriteLine();
}

// Cleanup
database.RemoveChangeListener();
database.Dispose();