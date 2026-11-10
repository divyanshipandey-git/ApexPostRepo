using System.Net.NetworkInformation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Console.Services;

/// <summary>
/// Listens to real operating system network events and feeds them into
/// <see cref="ISyncService.OnConnectivityChangedAsync"/>. When the network comes back the sync service
/// starts a sync; when it goes away the app switches to offline mode.
/// </summary>
public sealed class NetworkConnectivityMonitor : IHostedService, IDisposable
{
    private readonly ISyncService _syncService;
    private readonly ILogger<NetworkConnectivityMonitor> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool? _lastReported;

    public NetworkConnectivityMonitor(ISyncService syncService, ILogger<NetworkConnectivityMonitor> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        NetworkChange.NetworkAvailabilityChanged += OnAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnAddressChanged;

        // Establish the initial state so the UI is correct from the first screen.
        await ReportAsync(NetworkInterface.GetIsNetworkAvailable(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        NetworkChange.NetworkAvailabilityChanged -= OnAvailabilityChanged;
        NetworkChange.NetworkAddressChanged -= OnAddressChanged;
        return Task.CompletedTask;
    }

    private void OnAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) =>
        _ = ReportSafeAsync(e.IsAvailable);

    // Address changes (Wi-Fi switch, VPN) do not always raise an availability event.
    private void OnAddressChanged(object? sender, EventArgs e) =>
        _ = ReportSafeAsync(NetworkInterface.GetIsNetworkAvailable());

    private async Task ReportSafeAsync(bool isAvailable)
    {
        try
        {
            await ReportAsync(isAvailable, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process a connectivity change");
        }
    }

    private async Task ReportAsync(bool isAvailable, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_lastReported == isAvailable)
                return; // address changes fire repeatedly; only act on a real transition

            _lastReported = isAvailable;
            _logger.LogInformation("Network {State}", isAvailable ? "available" : "unavailable");
            await _syncService.OnConnectivityChangedAsync(isAvailable, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
