
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services.MoneyOrders;

public sealed class MoneyOrderService
{
    private readonly IMoneyOrderRepository _moneyOrderRepository;
    private readonly MoneyOrderPolicy _moneyOrderPolicy;
    private readonly TimeProvider _timeProvider;

    public Money VerificationThreshold => _moneyOrderPolicy.VerificationThreshold;

    public MoneyOrderService(
        IMoneyOrderRepository moneyOrderRepository,
        MoneyOrderPolicy moneyOrderPolicy,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(moneyOrderRepository);
        ArgumentNullException.ThrowIfNull(moneyOrderPolicy);

        _moneyOrderRepository = moneyOrderRepository;
        _moneyOrderPolicy = moneyOrderPolicy;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MoneyOrder> CreateAsync(
        string id,
        Money amount,
        string senderName,
        string recipientName,
        Money fee,
        IdentityVerification? identityVerification,
        BranchCode branchCode,
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        ArgumentNullException.ThrowIfNull(fee);
        ArgumentNullException.ThrowIfNull(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        cancellationToken.ThrowIfCancellationRequested();

        var moneyOrder = PrepareForTransaction(
            id,
            amount,
            senderName,
            recipientName,
            fee,
            identityVerification,
            branchCode,
            transactionId);

        await _moneyOrderRepository.AddAsync(
            moneyOrder,
            cancellationToken);

        return moneyOrder;
    }

    public MoneyOrder PrepareForTransaction(
        string id,
        Money amount,
        string senderName,
        string recipientName,
        Money fee,
        IdentityVerification? identityVerification,
        BranchCode branchCode,
        string transactionId)
    {
        ArgumentNullException.ThrowIfNull(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

        var moneyOrder = Prepare(
            id,
            amount,
            senderName,
            recipientName,
            fee,
            identityVerification);
        moneyOrder.AttachToTransaction(
            branchCode,
            transactionId,
            identityVerification,
            _timeProvider.GetUtcNow());
        return moneyOrder;
    }

    public Task SavePreparedAsync(
        MoneyOrder moneyOrder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moneyOrder);
        if (string.IsNullOrWhiteSpace(moneyOrder.TransactionId) ||
            string.IsNullOrWhiteSpace(moneyOrder.BranchCode))
        {
            throw new InvalidOperationException("Only a transaction-linked money order can be saved through this workflow.");
        }

        return _moneyOrderRepository.AddAsync(moneyOrder, cancellationToken);
    }

    public Task DeleteAsync(
        string moneyOrderId,
        CancellationToken cancellationToken = default) =>
        _moneyOrderRepository.DeleteAsync(moneyOrderId, cancellationToken);

    public bool RequiresIdentityVerification(MoneyOrder moneyOrder) =>
        _moneyOrderPolicy.RequiresIdentityVerification(moneyOrder);

    private MoneyOrder Prepare(
        string id,
        Money amount,
        string senderName,
        string recipientName,
        Money fee,
        IdentityVerification? identityVerification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        ArgumentNullException.ThrowIfNull(fee);

        var moneyOrder = new MoneyOrder(
            id.Trim(),
            amount,
            senderName.Trim(),
            recipientName.Trim(),
            fee);

        if (!_moneyOrderPolicy.CanIssue(moneyOrder, identityVerification))
        {
            throw new InvalidOperationException(
                "The money order cannot be issued without successful identity verification when required.");
        }

        return moneyOrder;
    }
}
