
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class JsonMoneyOrderRepository : IMoneyOrderRepository
{
    private readonly JsonFileStore<MoneyOrder> _store;

    public JsonMoneyOrderRepository(JsonPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _store = new JsonFileStore<MoneyOrder>(
            options.GetFilePath("money-orders.json"),
            moneyOrder => moneyOrder.Id);
    }

    public Task<MoneyOrder?> GetByIdAsync(
        string moneyOrderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moneyOrderId);

        return _store.GetByKeyAsync(
            moneyOrderId.Trim(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<MoneyOrder>> GetByBranchAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);

        var moneyOrders = await _store.GetAllAsync(cancellationToken);

        return moneyOrders
            .Where(moneyOrder => string.Equals(
                JsonEntityPropertyReader.GetBranchCode(moneyOrder),
                branchCode.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public Task AddAsync(
        MoneyOrder moneyOrder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moneyOrder);
        return _store.AddAsync(moneyOrder, cancellationToken);
    }

    public Task UpdateAsync(
        MoneyOrder moneyOrder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moneyOrder);
        return _store.UpdateAsync(moneyOrder, cancellationToken);
    }
}
