
using System.Collections.Concurrent;
using ApexPost.Application.Abstractions.Notifications;
using ApexPost.Application.Notifications;

namespace ApexPost.Application.Services.Notifications;

public sealed class SimulatedNotificationService : INotificationService
{
    private readonly ConcurrentQueue<NotificationMessage> _notifications = new();

    public event Action<NotificationMessage>? NotificationPublished;

    public IReadOnlyList<NotificationMessage> GetPublishedNotifications() =>
        _notifications.ToArray();

    public Task<bool> NotifyLowStockAsync(
        string branchCode,
        string sku,
        int currentQuantity,
        int reorderLevel,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(branchCode, nameof(branchCode));
        ValidateRequired(sku, nameof(sku));

        if (currentQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(currentQuantity));

        if (reorderLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(reorderLevel));

        cancellationToken.ThrowIfCancellationRequested();

        if (currentQuantity > reorderLevel)
            return Task.FromResult(false);

        Publish(new NotificationMessage(
            NotificationType.LowStock,
            "Low stock alert",
            $"Product {sku.Trim().ToUpperInvariant()} has {currentQuantity} units remaining. Reorder level: {reorderLevel}.",
            NormalizeBranch(branchCode),
            DateTimeOffset.UtcNow));

        return Task.FromResult(true);
    }

    public Task<bool> NotifyHighValueMoneyOrderAsync(
        string branchCode,
        string moneyOrderId,
        decimal amount,
        decimal verificationThreshold,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(branchCode, nameof(branchCode));
        ValidateRequired(moneyOrderId, nameof(moneyOrderId));

        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        if (verificationThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(verificationThreshold));

        cancellationToken.ThrowIfCancellationRequested();

        if (amount <= verificationThreshold)
            return Task.FromResult(false);

        Publish(new NotificationMessage(
            NotificationType.HighValueMoneyOrder,
            "High-value money order",
            $"Money order {moneyOrderId.Trim()} has an amount of {amount:F2} and requires attention under the applicable verification policy.",
            NormalizeBranch(branchCode),
            DateTimeOffset.UtcNow));

        return Task.FromResult(true);
    }

    public Task NotifyTransactionVoidAsync(
        string branchCode,
        string transactionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        PublishTransactionAction(
            NotificationType.TransactionVoid,
            "Transaction voided",
            branchCode,
            transactionId,
            reason,
            cancellationToken);

        return Task.CompletedTask;
    }

    public Task NotifyTransactionRefundAsync(
        string branchCode,
        string transactionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        PublishTransactionAction(
            NotificationType.TransactionRefund,
            "Transaction refunded",
            branchCode,
            transactionId,
            reason,
            cancellationToken);

        return Task.CompletedTask;
    }

    private void PublishTransactionAction(
        NotificationType type,
        string subject,
        string branchCode,
        string transactionId,
        string reason,
        CancellationToken cancellationToken)
    {
        ValidateRequired(branchCode, nameof(branchCode));
        ValidateRequired(transactionId, nameof(transactionId));
        ValidateRequired(reason, nameof(reason));
        cancellationToken.ThrowIfCancellationRequested();

        Publish(new NotificationMessage(
            type,
            subject,
            $"Transaction {transactionId.Trim()} was processed. Reason: {reason.Trim()}",
            NormalizeBranch(branchCode),
            DateTimeOffset.UtcNow));
    }

    private void Publish(NotificationMessage notification)
    {
        _notifications.Enqueue(notification);
        NotificationPublished?.Invoke(notification);
    }

    private static string NormalizeBranch(string branchCode) =>
        branchCode.Trim().ToUpperInvariant();

    private static void ValidateRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
    }
}
