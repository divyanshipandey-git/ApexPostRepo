namespace ApexPost.UnitTests.TestSupport;

/// <summary>A clock that only moves when the test says so (no DateTime.Now anywhere).</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
