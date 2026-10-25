
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public class CounterTests
{
    [Fact]
    public void Constructor_ShouldCreateActiveCounter()
    {
        var counter = CreateCounter();

        Assert.Equal("C001", counter.Id);
        Assert.Equal("IND01", counter.BranchCode.Value);
        Assert.Equal("Counter 1", counter.Name);
        Assert.True(counter.IsActive);
    }

    [Fact]
    public void Constructor_ShouldTrimIdAndName()
    {
        var counter = new Counter(
            " C001 ",
            new BranchCode("IND01"),
            " Counter 1 ");

        Assert.Equal("C001", counter.Id);
        Assert.Equal("Counter 1", counter.Name);
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyId()
    {
        Assert.Throws<ArgumentException>(() =>
            new Counter(" ", new BranchCode("IND01"), "Counter 1"));
    }

    [Fact]
    public void Deactivate_ShouldMakeCounterInactive()
    {
        var counter = CreateCounter();

        counter.Deactivate();

        Assert.False(counter.IsActive);
    }

    [Fact]
    public void Activate_ShouldReactivateCounter()
    {
        var counter = CreateCounter();
        counter.Deactivate();

        counter.Activate();

        Assert.True(counter.IsActive);
    }

    private static Counter CreateCounter() =>
        new("C001", new BranchCode("IND01"), "Counter 1");
}
