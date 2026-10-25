namespace ApexPost.Application.Services.Shipping;

public sealed record CourierStatusUpdateResult(
    CourierStatusUpdateStatus Status,
    string? ShipmentId = null,
    string? CourierStatus = null,
    DateTimeOffset? ReceivedAtUtc = null);
