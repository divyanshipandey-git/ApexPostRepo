using ApexPost.Domain.ValueObjects;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApexPost.Domain.Entities;

public sealed class Branch
{
    public BranchCode Code { get; private set; }
    public string Name { get; private set; }
    public string Region { get; private set; }
    public TimeOnly OpeningTime { get; private set; }
    public TimeOnly ClosingTime { get; private set; }
    [NotMapped]
    public Address? Address { get; private set; }
    [JsonInclude]
    public bool IsActive { get; private set; }

    // Private constructor for EF Core materialization
    private Branch()
    {
        Code = default!;
        Name = default!;
        Region = default!;
        OpeningTime = default;
        ClosingTime = default;
    }

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

    public void UpdateAddress(Address address)
    {
        ArgumentNullException.ThrowIfNull(address);
        Address = address;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public bool IsOpenAt(TimeOnly time) =>
        IsActive && time >= OpeningTime && time < ClosingTime;
}