using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models.Shipping;

public sealed class CourierStatusUpdateRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Status { get; set; } = string.Empty;
}

public sealed record CourierStatusUpdateResponse(
    string ShipmentId,
    string Status,
    DateTimeOffset ReceivedAtUtc);
