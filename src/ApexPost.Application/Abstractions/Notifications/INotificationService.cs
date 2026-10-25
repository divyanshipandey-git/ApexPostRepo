
using ApexPost.Application.Notifications;

namespace ApexPost.Application.Abstractions.Notifications;

public interface INotificationService
{
    Task<bool> NotifyLowStockAsync(
        string branchCode,
        string sku,
        int currentQuantity,
        int reorderLevel,
        CancellationToken cancellationToken = default);

    Task<bool> NotifyHighValueMoneyOrderAsync(
        string branchCode,
        string moneyOrderId,
        decimal amount,
        decimal verificationThreshold,
        CancellationToken cancellationToken = default);

    Task NotifyTransactionVoidAsync(
        string branchCode,
        string transactionId,
        string reason,
        CancellationToken cancellationToken = default);

    Task NotifyTransactionRefundAsync(
        string branchCode,
        string transactionId,
        string reason,
        CancellationToken cancellationToken = default);
}
