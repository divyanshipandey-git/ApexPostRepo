
using ApexPost.Application.Notifications;
using ApexPost.Application.Services.Notifications;
using Xunit;

namespace ApexPost.Application.Tests.Services.Notifications;

public sealed class SimulatedNotificationServiceTests
{
    [Fact]
    public async Task NotifyLowStockAsync_WhenQuantityAtReorderLevel_PublishesNotification()
    {
        var service = new SimulatedNotificationService();

        var published = await service.NotifyLowStockAsync(
            "ind01", "sku001", 5, 5);

        var notification = Assert.Single(service.GetPublishedNotifications());

        Assert.True(published);
        Assert.Equal(NotificationType.LowStock, notification.Type);
        Assert.Equal("IND01", notification.BranchCode);
        Assert.Contains("SKU001", notification.Message);
    }

    [Fact]
    public async Task NotifyLowStockAsync_WhenQuantityAboveReorderLevel_DoesNotPublish()
    {
        var service = new SimulatedNotificationService();

        var published = await service.NotifyLowStockAsync(
            "IND01", "SKU001", 10, 5);

        Assert.False(published);
        Assert.Empty(service.GetPublishedNotifications());
    }

    [Fact]
    public async Task NotifyLowStockAsync_WhenQuantityIsNegative_Throws()
    {
        var service = new SimulatedNotificationService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.NotifyLowStockAsync("IND01", "SKU001", -1, 5));
    }

    [Fact]
    public async Task NotifyHighValueMoneyOrderAsync_WhenAmountExceedsThreshold_Publishes()
    {
        var service = new SimulatedNotificationService();

        var published = await service.NotifyHighValueMoneyOrderAsync(
            "IND01", "MO001", 1500m, 1000m);

        var notification = Assert.Single(service.GetPublishedNotifications());

        Assert.True(published);
        Assert.Equal(NotificationType.HighValueMoneyOrder, notification.Type);
        Assert.Contains("MO001", notification.Message);
    }

    [Fact]
    public async Task NotifyHighValueMoneyOrderAsync_WhenAmountEqualsThreshold_DoesNotPublish()
    {
        var service = new SimulatedNotificationService();

        var published = await service.NotifyHighValueMoneyOrderAsync(
            "IND01", "MO001", 1000m, 1000m);

        Assert.False(published);
        Assert.Empty(service.GetPublishedNotifications());
    }

    [Fact]
    public async Task NotifyTransactionVoidAsync_PublishesVoidNotification()
    {
        var service = new SimulatedNotificationService();

        await service.NotifyTransactionVoidAsync(
            "IND01", "TX001", "Incorrect item");

        var notification = Assert.Single(service.GetPublishedNotifications());

        Assert.Equal(NotificationType.TransactionVoid, notification.Type);
        Assert.Contains("Incorrect item", notification.Message);
    }

    [Fact]
    public async Task NotifyTransactionRefundAsync_PublishesRefundNotification()
    {
        var service = new SimulatedNotificationService();

        await service.NotifyTransactionRefundAsync(
            "IND01", "TX002", "Customer refund");

        var notification = Assert.Single(service.GetPublishedNotifications());

        Assert.Equal(NotificationType.TransactionRefund, notification.Type);
        Assert.Contains("Customer refund", notification.Message);
    }

    [Fact]
    public async Task NotificationPublished_EventIsRaised()
    {
        var service = new SimulatedNotificationService();
        NotificationMessage? received = null;
        service.NotificationPublished += message => received = message;

        await service.NotifyLowStockAsync("IND01", "SKU001", 2, 5);

        Assert.NotNull(received);
        Assert.Equal(NotificationType.LowStock, received.Type);
    }

    [Fact]
    public async Task NotifyTransactionVoidAsync_WhenReasonIsBlank_Throws()
    {
        var service = new SimulatedNotificationService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.NotifyTransactionVoidAsync("IND01", "TX001", " "));
    }

    [Fact]
    public async Task NotifyLowStockAsync_WhenCancelled_Throws()
    {
        var service = new SimulatedNotificationService();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.NotifyLowStockAsync(
                "IND01", "SKU001", 2, 5, cancellation.Token));

        Assert.Empty(service.GetPublishedNotifications());
    }
}
