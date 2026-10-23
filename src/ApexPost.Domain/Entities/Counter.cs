
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class Counter
{
    public string Id { get; }
    public BranchCode BranchCode { get; }
    public string Name { get; }
    public bool IsActive { get; private set; }

    public Counter(
        string id,
        BranchCode branchCode,
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id.Trim();
        BranchCode = branchCode;
        Name = name.Trim();
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
