
namespace ApexPost.Application.Notifications;

public sealed record NotificationMessage(
    NotificationType Type,
    string Subject,
    string Message,
    string BranchCode,
    DateTimeOffset CreatedAt);
