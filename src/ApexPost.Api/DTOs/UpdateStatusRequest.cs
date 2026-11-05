using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApexPost.Api.DTOs;

/// <summary>
/// Request to update a shipment's status.
/// </summary>
public sealed class UpdateStatusRequest
{
    /// <summary>
    /// The new status for the shipment.
    /// </summary>
    [Required]
    [JsonPropertyName("status")]
    [EnumDataType(typeof(ShipmentStatus), ErrorMessage = "Invalid status value")]
    public ShipmentStatus Status { get; init; }

    /// <summary>
    /// Optional reason for the status change (required for Delivered/Failed).
    /// </summary>
    [StringLength(500)]
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// Optional recipient signature name (required when status is Delivered).
    /// </summary>
    [StringLength(100)]
    [JsonPropertyName("recipientSignature")]
    public string? RecipientSignature { get; init; }

    /// <summary>
    /// Optional GPS coordinates of the status change location.
    /// </summary>
    [JsonPropertyName("location")]
    public GpsCoordinates? Location { get; init; }
}

/// <summary>
/// GPS coordinates for location tracking.
/// </summary>
public sealed class GpsCoordinates
{
    /// <summary>
    /// Latitude in decimal degrees (-90 to 90).
    /// </summary>
    [Range(-90.0, 90.0)]
    [JsonPropertyName("latitude")]
    public double Latitude { get; init; }

    /// <summary>
    /// Longitude in decimal degrees (-180 to 180).
    /// </summary>
    [Range(-180.0, 180.0)]
    [JsonPropertyName("longitude")]
    public double Longitude { get; init; }
}

/// <summary>
/// Shipment status enumeration matching the allowed transitions.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShipmentStatus
{
    Created = 0,
    PickedUp = 1,
    InTransit = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Failed = 5
}