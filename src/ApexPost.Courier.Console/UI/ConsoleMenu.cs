using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ApexPost.Courier.Console.Models;
using ApexPost.Courier.Console.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Courier.Console.UI;

/// <summary>
/// Main console menu for the courier application.
/// </summary>
public sealed class ConsoleMenu
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ConsoleMenu> _logger;
    private readonly ITokenStorage _tokenStorage;
    private readonly IApiEndpointResolver _endpoint;
    private readonly ISyncService _syncService;
    private readonly ICourierDatabase _database;
    private readonly CourierOptions _options;
    private readonly CancellationTokenSource _cts = new();
    
    private string _currentCourierId = string.Empty;
    private string _currentRole = "Courier";
    private bool _isRunning = true;

    public ConsoleMenu(
        IServiceProvider services,
        ILogger<ConsoleMenu> logger,
        ITokenStorage tokenStorage,
        IApiEndpointResolver endpoint,
        ISyncService syncService,
        ICourierDatabase database,
        IOptions<CourierOptions> options)
    {
        _services = services;
        _logger = logger;
        _tokenStorage = tokenStorage;
        _endpoint = endpoint;
        _syncService = syncService;
        _database = database;
        _options = options.Value;
    }

    public async Task RunAsync()
    {
        _syncService.SyncStatusChanged += OnSyncStatusChanged;
        _syncService.PendingScansCountChanged += OnPendingCountChanged;

        System.Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            _isRunning = false;
            _cts.Cancel();
        };

        // Try to load existing token
        await CheckExistingTokenAsync();

        // Start auto-sync timer
        _ = Task.Run(() => AutoSyncLoopAsync(_cts.Token));

        while (_isRunning)
        {
            // D10: after a 401 the sync service clears the stored token. Detect that here and fall back to the login menu.
            if (_currentCourierId != string.Empty && !await _tokenStorage.HasValidTokenAsync())
            {
                _currentCourierId = string.Empty;
                _currentRole = "Courier";
                WriteError("Session expired or rejected by the server (401) - please log in again.");
                await Task.Delay(System.Console.IsOutputRedirected ? 0 : 1500);
            }

            ShowMainMenu();
            await ProcessInputAsync();
        }

        _logger.LogInformation("Application shutting down...");
    }

    private async Task CheckExistingTokenAsync()
    {
        if (await _tokenStorage.HasValidTokenAsync())
        {
            var token = await _tokenStorage.GetTokenAsync();
            if (token != null)
            {
                _currentCourierId = token.CourierId;
                _currentRole = token.Role;
                WriteSuccess($"Loaded existing token for {token.CourierId} ({token.Role})");
            }
        }
    }

    private void ShowMainMenu()
    {
        ClearScreen();
        WriteHeader("ApexPost Courier Console");
        WriteInfo($"Courier: {(_currentCourierId != string.Empty ? _currentCourierId : "Not logged in")} | Role: {_currentRole}");
        WriteOfflineBanner();
        WriteInfo($"Sync Status: {_syncService.CurrentStatus} | Pending: {_syncService.PendingScansCount} | Last Sync: {(_syncService.LastSyncTime?.ToLocalTime().ToString("g") ?? "Never")}");
        System.Console.WriteLine();

        if (_currentCourierId == string.Empty)
        {
            System.Console.WriteLine("1. Login / Get Token");
            System.Console.WriteLine("2. Paste Existing Token");
            System.Console.WriteLine("3. Exit");
        }
        else
        {
            System.Console.WriteLine("1. New Scan");
            System.Console.WriteLine("2. Pending Scans");
            System.Console.WriteLine("3. Sync Status");
            System.Console.WriteLine("4. Manual Sync Now");
            System.Console.WriteLine("5. List All Scans");
            System.Console.WriteLine("6. Logout");
            System.Console.WriteLine("7. Exit");
        }
        System.Console.WriteLine();
        System.Console.Write("Select option: ");
    }

    private async Task ProcessInputAsync()
    {
        var input = System.Console.ReadLine()?.Trim();
        if (input is null)
        {
            // End of input (stdin closed): stop instead of looping on "invalid option".
            _isRunning = false;
            return;
        }
        
        if (_currentCourierId == string.Empty)
        {
            switch (input)
            {
                case "1":
                    await LoginAsync();
                    break;
                case "2":
                    await PasteTokenAsync();
                    break;
                case "3":
                    _isRunning = false;
                    break;
                default:
                    WriteError("Invalid option. Please try again.");
                    await Task.Delay(1000);
                    break;
            }
        }
        else
        {
            switch (input)
            {
                case "1":
                    await NewScanAsync();
                    break;
                case "2":
                    await PendingScansAsync();
                    break;
                case "3":
                    await ShowSyncStatusAsync();
                    break;
                case "4":
                    await ManualSyncAsync();
                    break;
                case "5":
                    await ListAllScansAsync();
                    break;
                case "6":
                    await LogoutAsync();
                    break;
                case "7":
                    _isRunning = false;
                    break;
                default:
                    WriteError("Invalid option. Please try again.");
                    await Task.Delay(1000);
                    break;
            }
        }
    }

    private static void WaitForKey()
    {
        // Console.ReadKey throws when stdin is redirected (tests, CI, piped demos).
        if (!System.Console.IsInputRedirected) System.Console.ReadKey();
        else System.Console.ReadLine();
    }

    private static void ClearScreen()
    {
        // Console.Clear throws when stdout is redirected.
        if (!System.Console.IsOutputRedirected) System.Console.Clear();
    }

    private async Task LoginAsync()
    {
        ClearScreen();
        WriteHeader("Login - Get Development Token");

        System.Console.Write("Enter Courier ID: ");
        var courierId = System.Console.ReadLine()?.Trim();
        
        if (string.IsNullOrWhiteSpace(courierId))
        {
            WriteError("Courier ID is required.");
            await Task.Delay(1500);
            return;
        }

        System.Console.Write("Enter Role (Courier/Admin) [Courier]: ");
        var role = System.Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(role))
            role = "Courier";

        if (role != "Courier" && role != "Admin")
        {
            WriteError("Role must be 'Courier' or 'Admin'.");
            await Task.Delay(1500);
            return;
        }

        try
        {
            WriteInfo("Requesting token from /dev/token endpoint...");
            
            var request = new DevTokenRequestDto { CourierId = courierId, Role = role };
            var json = JsonSerializer.Serialize(request, ApexJsonContext.Default.DevTokenRequestDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            using var httpClient = new HttpClient();
            httpClient.BaseAddress = _endpoint.BaseAddress;
            
            var response = await httpClient.PostAsync("/dev/token", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize(responseJson, ApexJsonContext.Default.DevTokenResponseDto)
                    ?? throw new InvalidOperationException("Empty token response");

                var tokenData = new TokenData
                {
                    AccessToken = tokenResponse.AccessToken,
                    TokenType = "Bearer",
                    ExpiresIn = tokenResponse.ExpiresIn,
                    CourierId = courierId,
                    Role = role
                };

                await _tokenStorage.StoreTokenAsync(tokenData);
                
                _currentCourierId = courierId;
                _currentRole = role;
                
                WriteSuccess($"Token obtained and stored for {courierId} ({role})");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                WriteError($"Failed to obtain token: {response.StatusCode} - {error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            WriteError($"Error: {ex.Message}");
        }

        await Task.Delay(2000);
    }

    private async Task PasteTokenAsync()
    {
        ClearScreen();
        WriteHeader("Paste Existing Token");

        System.Console.WriteLine("Paste your JWT token below (press Enter twice when done):");
        var lines = new List<string>();
        while (true)
        {
            var line = System.Console.ReadLine();
            if (string.IsNullOrEmpty(line))
                break;
            lines.Add(line);
        }

        var token = string.Join("", lines).Trim();
        
        if (string.IsNullOrWhiteSpace(token))
        {
            WriteError("No token provided.");
            await Task.Delay(1500);
            return;
        }

        try
        {
            // Parse token to get courier ID and role
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                WriteError("Invalid JWT format.");
                await Task.Delay(1500);
                return;
            }

            // JWTs are base64url encoded (- and _ instead of + and /, no padding)
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var payloadBytes = Convert.FromBase64String(payload);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            using var doc = JsonDocument.Parse(payloadJson);

            // The API puts the role under the long ClaimTypes.Role URI; tolerate a short "role" claim too.
            static string? Claim(JsonElement root, params string[] names)
            {
                foreach (var n in names)
                    if (root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                        return v.GetString();
                return null;
            }

            var courierId = Claim(doc.RootElement, "courierId") ?? "UNKNOWN";
            var role = Claim(doc.RootElement, "role", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "Courier";

            var tokenData = new TokenData
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = 28800,
                CourierId = courierId,
                Role = role
            };

            await _tokenStorage.StoreTokenAsync(tokenData);
            
            _currentCourierId = courierId;
            _currentRole = role;
            
            WriteSuccess($"Token stored for {courierId} ({role})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing pasted token");
            WriteError($"Error parsing token: {ex.Message}");
        }

        await Task.Delay(2000);
    }

    private async Task LogoutAsync()
    {
        await _tokenStorage.ClearTokenAsync();
        _currentCourierId = string.Empty;
        _currentRole = "Courier";
        WriteSuccess("Logged out successfully. Token cleared.");
        await Task.Delay(1500);
    }

    private async Task NewScanAsync()
    {
        ClearScreen();
        WriteHeader("New Scan");

        System.Console.Write("Tracking Number (APX-NNNNNNNN): ");
        var trackingNumber = System.Console.ReadLine()?.Trim();
        
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            WriteError("Tracking number is required.");
            await Task.Delay(1500);
            return;
        }

        System.Console.WriteLine("Scan Types: 1=Pickup, 2=InTransit, 3=Delivered, 4=Failed");
        System.Console.Write("Select Scan Type [1]: ");
        var scanTypeInput = System.Console.ReadLine()?.Trim();
        var scanType = scanTypeInput switch
        {
            "2" => ScanType.InTransit,
            "3" => ScanType.Delivered,
            "4" => ScanType.Failed,
            _ => ScanType.Pickup
        };

        System.Console.Write($"Courier ID [{_currentCourierId}]: ");
        var courierId = System.Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(courierId))
            courierId = _currentCourierId;

        System.Console.Write("Location: ");
        var location = System.Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(location))
        {
            WriteError("Location is required.");
            await Task.Delay(1500);
            return;
        }

        System.Console.Write("Notes (optional): ");
        var notes = System.Console.ReadLine()?.Trim();

        try
        {
            var scan = LocalScanDocument.Create(
                trackingNumber: trackingNumber,
                scanType: scanType,
                courierId: courierId,
                location: location,
                scannedAtUtc: DateTimeOffset.UtcNow,
                notes: notes);

            await _database.AddScanAsync(scan);
            
            WriteSuccess($"Scan saved locally with ID: {scan.ScanId}");
            WriteInfo("Status: Pending (will sync when online)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving scan");
            WriteError($"Error: {ex.Message}");
        }

        await Task.Delay(2000);
    }

    private async Task PendingScansAsync()
    {
        ClearScreen();
        WriteHeader("Pending Scans");

        var scans = await _database.GetPendingScansAsync();
        
        if (scans.Count == 0)
        {
            WriteInfo("No pending scans.");
        }
        else
        {
            System.Console.WriteLine($"{"Scan ID",-50} {"Tracking",-15} {"Type",-10} {"Courier",-15} {"Location",-20} {"Time"}");
            System.Console.WriteLine(new string('-', 120));
            
            foreach (var scan in scans.OrderBy(s => s.ScannedAtUtc))
            {
                var shortId = scan.ScanId.Length > 48 ? scan.ScanId[..48] : scan.ScanId;
                var shortLoc = scan.Location.Length > 18 ? scan.Location[..18] : scan.Location;
                System.Console.WriteLine($"{shortId,-50} {scan.TrackingNumber,-15} {scan.ScanType,-10} {scan.CourierId,-15} {shortLoc,-20} {scan.ScannedAtUtc:g}");
            }
            
            System.Console.WriteLine();
            System.Console.WriteLine($"Total: {scans.Count} pending scan(s)");
        }

        System.Console.WriteLine("\nPress any key to continue...");
        WaitForKey();
    }

    private async Task ShowSyncStatusAsync()
    {
        ClearScreen();
        WriteHeader("Sync Status");
        WriteOfflineBanner();

        var statusColor = _syncService.CurrentStatus switch
        {
            SyncState.Idle => ConsoleColor.Gray,
            SyncState.Syncing => ConsoleColor.Blue,
            SyncState.Offline => ConsoleColor.DarkYellow,
            SyncState.CircuitOpen => ConsoleColor.Red,
            SyncState.Error => ConsoleColor.DarkRed,
            _ => ConsoleColor.Gray
        };

        var originalColor = System.Console.ForegroundColor;
        System.Console.ForegroundColor = statusColor;
        System.Console.WriteLine($"Current Status: {_syncService.CurrentStatus}");
        System.Console.ForegroundColor = originalColor;

        System.Console.WriteLine($"Last Sync: {(_syncService.LastSyncTime?.ToLocalTime().ToString("g") ?? "Never")}");
        System.Console.WriteLine($"Pending Scans: {_syncService.PendingScansCount}");
        System.Console.WriteLine($"Circuit Breaker: {(_syncService.CurrentStatus == SyncState.CircuitOpen ? "OPEN" : "Closed")}");
        System.Console.WriteLine($"Connectivity: {(_syncService.CurrentStatus == SyncState.Offline ? "Offline" : "Online")}");

        System.Console.WriteLine("\nPress any key to continue...");
        WaitForKey();
    }

    private async Task ManualSyncAsync()
    {
        ClearScreen();
        WriteHeader("Manual Sync");

        WriteInfo("Starting sync of pending scans...");
        
        var result = await _syncService.SyncPendingScansAsync();
        
        if (result.IsSuccess)
        {
            WriteSuccess($"Sync completed: {result.ScansSynced} synced, {result.ScansSkipped} skipped, {result.ScansFailed} failed");
        }
        else
        {
            WriteError($"Sync failed: {result.ErrorMessage ?? "Unknown error"}");
            System.Console.WriteLine($"Details: {result.ScansSynced} synced, {result.ScansSkipped} skipped, {result.ScansFailed} failed");
        }

        System.Console.WriteLine("\nPress any key to continue...");
        WaitForKey();
    }

    private async Task ListAllScansAsync()
    {
        ClearScreen();
        WriteHeader("All Scans");

        var scans = await _database.ListAllScansAsync();
        
        if (scans.Count == 0)
        {
            WriteInfo("No scans found.");
        }
        else
        {
            System.Console.WriteLine($"{"Scan ID",-50} {"Tracking",-15} {"Type",-10} {"Courier",-15} {"Status",-10} {"Time"}");
            System.Console.WriteLine(new string('-', 120));
            
            foreach (var scan in scans.OrderByDescending(s => s.ScannedAtUtc))
            {
                var shortId = scan.ScanId.Length > 48 ? scan.ScanId[..48] : scan.ScanId;
                System.Console.WriteLine($"{shortId,-50} {scan.TrackingNumber,-15} {scan.ScanType,-10} {scan.CourierId,-15} {scan.SyncStatus,-10} {scan.ScannedAtUtc:g}");
            }
            
            System.Console.WriteLine($"\nTotal: {scans.Count} scan(s)");
        }

        System.Console.WriteLine("\nPress any key to continue...");
        WaitForKey();
    }

    private async Task AutoSyncLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.AutoSyncIntervalSeconds), cancellationToken);
                
                if (!_cts.Token.IsCancellationRequested && IsConnected())
                {
                    var pending = await _database.GetPendingScansAsync();
                    if (pending.Count > 0)
                    {
                        WriteInfo($"Auto-sync: {pending.Count} pending scan(s) found, syncing...");
                        await _syncService.SyncPendingScansAsync(cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto-sync loop");
            }
        }
    }

    /// <summary>
    /// Shown whenever the device has no network or the circuit breaker says the server is down.
    /// Scans keep being saved locally and are uploaded automatically when the server is back.
    /// </summary>
    private void WriteOfflineBanner()
    {
        if (_syncService.CurrentStatus is not (SyncState.Offline or SyncState.CircuitOpen))
            return;

        var reason = _syncService.CurrentStatus == SyncState.Offline ? "no network connection" : "server not responding";
        System.Console.BackgroundColor = ConsoleColor.DarkYellow;
        System.Console.ForegroundColor = ConsoleColor.Black;
        System.Console.WriteLine($" Server unavailable - working offline ({reason}). Scans are saved on this device and will sync automatically. ");
        System.Console.ResetColor();
    }

    private bool IsConnected() => _syncService.CurrentStatus != SyncState.Offline && _syncService.CurrentStatus != SyncState.CircuitOpen;

    private void OnSyncStatusChanged(object? sender, SyncStatusChangedEventArgs e)
    {
        // Status change logged in SyncService. Tell the courier as soon as we go offline / come back.
        if (e.CurrentStatus is SyncState.Offline or SyncState.CircuitOpen)
        {
            WriteOfflineBanner();
        }
        else if (e.PreviousStatus is SyncState.Offline or SyncState.CircuitOpen)
        {
            System.Console.ForegroundColor = ConsoleColor.Green;
            System.Console.WriteLine("[ONLINE] Server reachable again - syncing pending scans.");
            System.Console.ResetColor();
        }
    }

    private void OnPendingCountChanged(object? sender, int count)
    {
        // Count change logged in SyncService
    }

    private static void WriteHeader(string text)
    {
        System.Console.WriteLine(new string('=', 60));
        System.Console.WriteLine(text.PadLeft((60 + text.Length) / 2));
        System.Console.WriteLine(new string('=', 60));
    }

    private static void WriteInfo(string text)
    {
        System.Console.ForegroundColor = ConsoleColor.Cyan;
        System.Console.WriteLine($"[INFO] {text}");
        System.Console.ResetColor();
    }

    private static void WriteSuccess(string text)
    {
        System.Console.ForegroundColor = ConsoleColor.Green;
        System.Console.WriteLine($"[SUCCESS] {text}");
        System.Console.ResetColor();
    }

    private static void WriteError(string text)
    {
        System.Console.ForegroundColor = ConsoleColor.Red;
        System.Console.WriteLine($"[ERROR] {text}");
        System.Console.ResetColor();
    }
}

public sealed class CourierOptions
{
    public string DefaultCourierId { get; set; } = "COURIER-001";
    public int SyncBatchSize { get; set; } = 20;
    public int AutoSyncIntervalSeconds { get; set; } = 30;
}