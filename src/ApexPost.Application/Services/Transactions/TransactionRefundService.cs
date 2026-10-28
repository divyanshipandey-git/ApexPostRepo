using ApexPost.Application.Abstractions.Notifications;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services.Transactions;

public sealed class TransactionRefundService
{
    private readonly ICounterTransactionRepository _transactionRepository;
    private readonly TransactionVoidService _accessService;
    private readonly INotificationService _notifications;

    public TransactionRefundService(
        ICounterTransactionRepository transactionRepository,
        TransactionVoidService accessService,
        INotificationService notifications)
    {
        ArgumentNullException.ThrowIfNull(transactionRepository);
        ArgumentNullException.ThrowIfNull(accessService);
        ArgumentNullException.ThrowIfNull(notifications);
        _transactionRepository = transactionRepository;
        _accessService = accessService;
        _notifications = notifications;
    }

    public async Task<TransactionRefundResult> RefundAsync(
        string transactionId,
        string? reason,
        string? staffId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return new TransactionRefundResult(TransactionRefundStatus.InvalidTransactionId);
        }

        using var reversalGate = await TransactionVoidService.AcquireReversalGateAsync(
            transactionId,
            cancellationToken);
        var accessible = await _accessService.GetAccessibleAsync(
            transactionId,
            staffId,
            cancellationToken);
        if (accessible.Status != TransactionVoidStatus.Available)
        {
            return new TransactionRefundResult(Map(accessible.Status), accessible.Transaction);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new TransactionRefundResult(TransactionRefundStatus.InvalidReason, accessible.Transaction);
        }

        var transaction = accessible.Transaction!;
        try
        {
            transaction.Refund(reason);
        }
        catch (InvalidOperationException)
        {
            return new TransactionRefundResult(TransactionRefundStatus.InvalidState, transaction);
        }
        catch (ArgumentException)
        {
            return new TransactionRefundResult(TransactionRefundStatus.InvalidReason, transaction);
        }

        await _transactionRepository.UpdateAsync(transaction, cancellationToken);
        await _accessService.RestoreSoldRetailStockAsync(transaction);
        await _notifications.NotifyTransactionRefundAsync(
            transaction.BranchCode.Value,
            transaction.Id,
            transaction.Reason!,
            CancellationToken.None);

        return new TransactionRefundResult(TransactionRefundStatus.Refunded, transaction);
    }

    private static TransactionRefundStatus Map(TransactionVoidStatus status) => status switch
    {
        TransactionVoidStatus.NotFound => TransactionRefundStatus.NotFound,
        TransactionVoidStatus.Forbidden => TransactionRefundStatus.Forbidden,
        TransactionVoidStatus.InvalidReason => TransactionRefundStatus.InvalidReason,
        TransactionVoidStatus.InvalidState => TransactionRefundStatus.InvalidState,
        TransactionVoidStatus.InvalidTransactionId => TransactionRefundStatus.InvalidTransactionId,
        _ => TransactionRefundStatus.InvalidState
    };
}
