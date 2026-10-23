
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class Branch
{
    public BranchCode Code { get; }
    public string Name { get; }
    public string Region { get; }
    public TimeOnly OpeningTime { get; }
    public TimeOnly ClosingTime { get; }
    public bool IsActive { get; private set; }

    public Branch(
        BranchCode code,
        string name,
        string region,
        TimeOnly openingTime,
        TimeOnly closingTime)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);

        if (closingTime <= openingTime)
        {
            throw new ArgumentException(
                "Closing time must be later than opening time.",
                nameof(closingTime));
        }

        Code = code;
        Name = name.Trim();
        Region = region.Trim();
        OpeningTime = openingTime;
        ClosingTime = closingTime;
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public bool IsOpenAt(TimeOnly time) =>
        IsActive && time >= OpeningTime && time < ClosingTime;
}
