
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services.MoneyOrders;

public sealed class MoneyOrderService
{
    private readonly IMoneyOrderRepository _moneyOrderRepository;
    private readonly MoneyOrderPolicy _moneyOrderPolicy;

    public MoneyOrderService(
        IMoneyOrderRepository moneyOrderRepository,
        MoneyOrderPolicy moneyOrderPolicy)
    {
        ArgumentNullException.ThrowIfNull(moneyOrderRepository);
        ArgumentNullException.ThrowIfNull(moneyOrderPolicy);

        _moneyOrderRepository = moneyOrderRepository;
        _moneyOrderPolicy = moneyOrderPolicy;
    }

    public async Task<MoneyOrder> CreateAsync(
        string id,
        Money amount,
        string senderName,
        string recipientName,
        Money fee,
        IdentityVerification? identityVerification,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        ArgumentNullException.ThrowIfNull(fee);
        cancellationToken.ThrowIfCancellationRequested();

        var moneyOrder = new MoneyOrder(
            id.Trim(),
            amount,
            senderName.Trim(),
            recipientName.Trim(),
            fee);

        if (!_moneyOrderPolicy.CanIssue(
                moneyOrder,
                identityVerification))
        {
            throw new InvalidOperationException(
                "The money order cannot be issued without successful identity verification when required.");
        }

        await _moneyOrderRepository.AddAsync(
            moneyOrder,
            cancellationToken);

        return moneyOrder;
    }
}
