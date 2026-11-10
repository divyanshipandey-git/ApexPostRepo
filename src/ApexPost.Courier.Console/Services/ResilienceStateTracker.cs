using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Console.Services;

/// <summary>
/// Receives the Polly circuit breaker callbacks and exposes the real circuit state to the rest of the
/// app (sync service, UI banner). Every transition is logged here.
/// </summary>
public interface IResilienceStateTracker
{
    bool IsCircuitOpen { get; }
    event EventHandler<bool>? CircuitStateChanged;

    void CircuitOpened(TimeSpan breakDuration, string? reason);
    void CircuitHalfOpened();
    void CircuitClosed();
}

public sealed class ResilienceStateTracker : IResilienceStateTracker
{
    private readonly ILogger<ResilienceStateTracker> _logger;
    private volatile bool _open;

    public event EventHandler<bool>? CircuitStateChanged;

    public ResilienceStateTracker(ILogger<ResilienceStateTracker> logger) => _logger = logger;

    public bool IsCircuitOpen => _open;

    public void CircuitOpened(TimeSpan breakDuration, string? reason)
    {
        _open = true;
        _logger.LogWarning("Circuit breaker OPENED for {BreakDuration}s: {Reason}", breakDuration.TotalSeconds, reason ?? "failure ratio exceeded");
        CircuitStateChanged?.Invoke(this, true);
    }

    public void CircuitHalfOpened()
    {
        _logger.LogInformation("Circuit breaker HALF-OPEN: the next request is a probe");
    }

    public void CircuitClosed()
    {
        _open = false;
        _logger.LogInformation("Circuit breaker CLOSED: the server is reachable again");
        CircuitStateChanged?.Invoke(this, false);
    }
}
