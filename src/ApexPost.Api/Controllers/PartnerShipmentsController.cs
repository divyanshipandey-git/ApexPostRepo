using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApexPost.Api.DTOs.v2;
using ApexPost.Api.Filters;
using ApexPost.Api.Routing;
using ApexPost.Api.Services;

namespace ApexPost.Api.Controllers;

/// <summary>
/// Read-only shipment access for partners. Authenticated with the <c>X-Api-Key</c> header, not a JWT.
/// </summary>
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/partner/shipments")]
[Tags("Partner")]
[ApiKeyAuthorize]
[EnableRateLimiting("ApiPolicy")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class PartnerShipmentsController : ControllerBase
{
    private readonly IShipmentApiService _shipments;

    public PartnerShipmentsController(IShipmentApiService shipments)
    {
        _shipments = shipments ?? throw new ArgumentNullException(nameof(shipments));
    }

    /// <summary>Get a shipment by tracking number (partner API). Requires the X-Api-Key header.</summary>
    /// <param name="trackingNumber">Tracking number in format APX-NNNNNNNN.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("{trackingNumber:" + TrackingNumberRouteConstraint.Name + "}", Name = nameof(GetPartnerShipment))]
    [EndpointSummary("Get a shipment (partner API key)")]
    [ProducesResponseType<V2ShipmentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<V2ShipmentDetail>> GetPartnerShipment(string trackingNumber, CancellationToken cancellationToken)
        => await _shipments.GetShipmentV2Async(trackingNumber, cancellationToken);
}
