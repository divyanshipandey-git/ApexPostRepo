using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;

namespace ApexPost.Specs.Support;

// Small in-memory stores for the service-level scenarios of money orders and staff sign-in.
// They hold data in dictionaries (no database, no files), so each scenario starts empty and cannot leak into the next.

public sealed class InMemoryMoneyOrderRepository : IMoneyOrderRepository
{
    private readonly Dictionary<string, MoneyOrder> _orders = new(StringComparer.Ordinal);

    public int Count => _orders.Count;

    public Task<MoneyOrder?> GetByIdAsync(string moneyOrderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_orders.GetValueOrDefault(moneyOrderId));

    public Task<IReadOnlyList<MoneyOrder>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MoneyOrder>>(
            _orders.Values.Where(order => order.BranchCode == branchCode).ToList());

    public Task AddAsync(MoneyOrder moneyOrder, CancellationToken cancellationToken = default)
    {
        _orders.Add(moneyOrder.Id, moneyOrder);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MoneyOrder moneyOrder, CancellationToken cancellationToken = default)
    {
        _orders[moneyOrder.Id] = moneyOrder;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string moneyOrderId, CancellationToken cancellationToken = default)
    {
        _orders.Remove(moneyOrderId);
        return Task.CompletedTask;
    }
}

public sealed class CountingUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        var result = await operation(cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return result;
    }
}

public sealed class InMemoryStaffRepository : IStaffRepository
{
    private readonly List<Staff> _staff = [];

    public Task<Staff?> GetByIdAsync(string staffId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_staff.FirstOrDefault(member => member.Id == staffId));

    public Task<IReadOnlyList<Staff>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Staff>>(_staff.ToList());

    public Task<IReadOnlyList<Staff>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Staff>>(_staff.Where(member => member.HomeBranchCode.Value == branchCode).ToList());

    public Task AddAsync(Staff staff, CancellationToken cancellationToken = default)
    {
        _staff.Add(staff);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Staff staff, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class InMemoryCredentialRepository : IStaffCredentialRepository
{
    private readonly Dictionary<string, string> _hashes = new(StringComparer.Ordinal);

    public Task AddPasswordHashAsync(string staffId, string passwordHash, CancellationToken cancellationToken = default)
    {
        _hashes[staffId] = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(string staffId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_hashes.GetValueOrDefault(staffId));
}
