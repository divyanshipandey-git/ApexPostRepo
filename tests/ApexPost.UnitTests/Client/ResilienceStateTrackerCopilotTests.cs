// COPILOT-ASSISTED: drafted with GitHub Copilot (prompt 6 in COPILOT-LOG.md), then reviewed and extended by the student.
using ApexPost.Courier.Console.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApexPost.UnitTests.Client;

public class ResilienceStateTrackerCopilotTests
{
    [Fact]
    public void NewTracker_IsCircuitOpenFalse()
    {
        // Arrange
        var tracker = new ResilienceStateTracker(NullLogger<ResilienceStateTracker>.Instance);

        // Act
        var isCircuitOpen = tracker.IsCircuitOpen;

        // Assert
        Assert.False(isCircuitOpen);
    }

    [Fact]
    public void CircuitOpened_WhenCalled_SetsCircuitOpenTrueAndRaisesEventOnce()
    {
        // Arrange
        var tracker = new ResilienceStateTracker(NullLogger<ResilienceStateTracker>.Instance);
        var eventCount = 0;
        bool? state = null;
        tracker.CircuitStateChanged += (_, value) =>
        {
            eventCount++;
            state = value;
        };

        // Act
        tracker.CircuitOpened(TimeSpan.FromSeconds(30), "failure ratio exceeded");

        // Assert
        Assert.True(tracker.IsCircuitOpen);
        Assert.Equal(1, eventCount);
        Assert.True(state);
    }

    [Fact]
    public void CircuitClosed_WhenCalled_SetsCircuitOpenFalseAndRaisesEventOnce()
    {
        // Arrange
        var tracker = new ResilienceStateTracker(NullLogger<ResilienceStateTracker>.Instance);
        var eventCount = 0;
        bool? state = null;
        tracker.CircuitStateChanged += (_, value) =>
        {
            eventCount++;
            state = value;
        };

        // Act
        tracker.CircuitClosed();

        // Assert
        Assert.False(tracker.IsCircuitOpen);
        Assert.Equal(1, eventCount);
        Assert.False(state);
    }

    [Fact]
    public void CircuitHalfOpened_WhenCalled_DoesNotChangeOpenStateOrRaiseEvent()
    {
        // Arrange
        var tracker = new ResilienceStateTracker(NullLogger<ResilienceStateTracker>.Instance);
        var eventCount = 0;
        tracker.CircuitStateChanged += (_, _) => eventCount++;

        // Act
        tracker.CircuitHalfOpened();

        // Assert
        Assert.False(tracker.IsCircuitOpen);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void CircuitOpened_WhenCalledTwice_RaisesEventTwice()
    {
        // Arrange
        var tracker = new ResilienceStateTracker(NullLogger<ResilienceStateTracker>.Instance);
        // Reviewed: Copilot asserted inside the event handler, so a failure would surface as an exception thrown from the
        // Act line. The values are collected here and asserted after the Act instead.
        var received = new List<bool>();
        tracker.CircuitStateChanged += (_, value) => received.Add(value);

        // Act
        tracker.CircuitOpened(TimeSpan.FromSeconds(30), "first failure");
        tracker.CircuitOpened(TimeSpan.FromSeconds(30), "second failure");

        // Assert
        Assert.Equal([true, true], received);
        Assert.True(tracker.IsCircuitOpen);
    }

    [Fact]
    public void CircuitStateChanged_WhenSubscribedAndUnsubscribed_EventsOnlyWhileSubscribed()
    {
        // Arrange
        var tracker = new ResilienceStateTracker(NullLogger<ResilienceStateTracker>.Instance);
        var received = new List<bool>();
        EventHandler<bool> handler = (_, value) => received.Add(value);

        tracker.CircuitStateChanged += handler;

        // Act
        tracker.CircuitOpened(TimeSpan.FromSeconds(30), "failure ratio exceeded");
        tracker.CircuitStateChanged -= handler;
        tracker.CircuitOpened(TimeSpan.FromSeconds(30), "failure ratio exceeded");

        // Assert
        Assert.Equal([true], received);
        Assert.True(tracker.IsCircuitOpen);
    }

    // ---- Added in review. Copilot's CircuitClosed test starts from a tracker that was never opened, so it passes even if
    // CircuitClosed forgot to reset the state (the initial value is already false). These cases test real transitions. ----

    [Fact]
    public void CircuitClosed_AfterOpened_ResetsTheStateAndRaisesTrueThenFalseInOrder()
    {
        // Arrange
        var tracker = new ResilienceStateTracker(NullLogger<ResilienceStateTracker>.Instance);
        var received = new List<bool>();
        tracker.CircuitStateChanged += (_, value) => received.Add(value);

        // Act
        tracker.CircuitOpened(TimeSpan.FromSeconds(30), "failure ratio exceeded");
        tracker.CircuitClosed();

        // Assert
        Assert.False(tracker.IsCircuitOpen);
        Assert.Equal([true, false], received);
    }

    [Fact]
    public void CircuitHalfOpened_AfterOpened_KeepsTheCircuitOpenAndRaisesNoExtraEvent()
    {
        // Arrange
        var tracker = new ResilienceStateTracker(NullLogger<ResilienceStateTracker>.Instance);
        var eventCount = 0;
        tracker.CircuitOpened(TimeSpan.FromSeconds(30), "failure ratio exceeded");
        tracker.CircuitStateChanged += (_, _) => eventCount++;

        // Act
        tracker.CircuitHalfOpened();

        // Assert
        Assert.True(tracker.IsCircuitOpen);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void CircuitOpened_WhenHandlerRuns_HandlerAlreadySeesTheNewState()
    {
        // Arrange
        var tracker = new ResilienceStateTracker(NullLogger<ResilienceStateTracker>.Instance);
        bool? stateSeenInsideHandler = null;
        tracker.CircuitStateChanged += (_, _) => stateSeenInsideHandler = tracker.IsCircuitOpen;

        // Act
        tracker.CircuitOpened(TimeSpan.FromSeconds(30), "failure ratio exceeded");

        // Assert
        Assert.True(stateSeenInsideHandler);
    }

    [Fact]
    public void CircuitOpened_WithoutReasonOrSubscribers_DoesNotThrow()
    {
        // Arrange
        var tracker = new ResilienceStateTracker(NullLogger<ResilienceStateTracker>.Instance);

        // Act
        var exception = Record.Exception(() => tracker.CircuitOpened(TimeSpan.Zero, null));

        // Assert
        Assert.Null(exception);
        Assert.True(tracker.IsCircuitOpen);
    }
}
