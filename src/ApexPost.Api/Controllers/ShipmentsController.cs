using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApexPost.Api.DTOs;
using ApexPost.Api.DTOs.v2;
using ApexPost.Api.Routing;
using ApexPost.Api.Services;

namespace ApexPost.Api.Controllers;

/// <summary>
/// Manages tracked shipments. Version 1 is deprecated; version 2 returns a nested recipient and the latest scan.
/// </summary>
/// <remarks>
/// The actions only translate HTTP to a service call. Validation is done by the framework
/// ([ApiController] + DataAnnotations on the DTOs) and business rules, not-found and conflict handling are done
/// by <see cref="IShipmentApiService"/>, which throws exceptions that the global exception handler turns into
/// problem details.
/// </remarks>
[ApiController]
[ApiVersion("1.0", Deprecated = true)]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/shipments")]
[Authorize]
[EnableRateLimiting("ApiPolicy")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class ShipmentsController : ControllerBase
{
    private const string TrackingNumberRoute = "{trackingNumber:" + TrackingNumberRouteConstraint.Name + "}";

    private readonly IShipmentApiService _shipments;

    public ShipmentsController(IShipmentApiService shipments)
    {
        _shipments = shipments ?? throw new ArgumentNullException(nameof(shipments));
    }

    // ------------------------------------------------------------------ list

    /// <summary>Get a paged list of shipments (v1).</summary>
    /// <param name="filter">Paging and filter parameters. Default page size is 20, maximum 50.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<PagedResponse<ShipmentSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ShipmentSummary>>> GetShipmentsV1(
        [FromQuery] ShipmentFilter filter, CancellationToken cancellationToken)
        => await _shipments.GetShipmentsAsync(filter, cancellationToken);

    /// <summary>Get a paged list of shipments with nested recipient and latest scan (v2).</summary>
    /// <param name="filter">Paging and filter parameters. Default page size is 20, maximum 50.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [MapToApiVersion("2.0")]
    [ProducesResponseType<V2PagedResponse<V2ShipmentSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<V2PagedResponse<V2ShipmentSummary>>> GetShipmentsV2(
        [FromQuery] ShipmentFilter filter, CancellationToken cancellationToken)
        => await _shipments.GetShipmentsV2Async(filter, cancellationToken);

    // ------------------------------------------------------------------- get

    /// <summary>Get one shipment by tracking number, including all scans (v1).</summary>
    /// <param name="trackingNumber">Tracking number in format APX-YYYYMMDD.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet(TrackingNumberRoute, Name = nameof(GetShipmentV1))]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<ShipmentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentDetail>> GetShipmentV1(string trackingNumber, CancellationToken cancellationToken)
        => await _shipments.GetShipmentAsync(trackingNumber, cancellationToken);

    /// <summary>Get one shipment with nested recipient and latest scan (v2).</summary>
    /// <param name="trackingNumber">Tracking number in format APX-YYYYMMDD.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet(TrackingNumberRoute, Name = nameof(GetShipmentV2))]
    [MapToApiVersion("2.0")]
    [ProducesResponseType<V2ShipmentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<V2ShipmentDetail>> GetShipmentV2(string trackingNumber, CancellationToken cancellationToken)
        => await _shipments.GetShipmentV2Async(trackingNumber, cancellationToken);

    // ---------------------------------------------------------------- create

    /// <summary>Create a shipment (v1).</summary>
    /// <param name="request">The shipment to create. The tracking number must be unique and match APX-YYYYMMDD.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<ShipmentDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShipmentDetail>> CreateShipmentV1(
        [FromBody] CreateShipmentRequest request, CancellationToken cancellationToken)
    {
        var created = await _shipments.CreateShipmentAsync(request, cancellationToken);
        return CreatedAtRoute(nameof(GetShipmentV1), new { version = "1", trackingNumber = created.TrackingNumber }, created);
    }

    /// <summary>Create a shipment (v2).</summary>
    /// <param name="request">The shipment to create. The tracking number must be unique and match APX-YYYYMMDD.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost]
    [MapToApiVersion("2.0")]
    [ProducesResponseType<V2ShipmentDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<V2ShipmentDetail>> CreateShipmentV2(
        [FromBody] CreateShipmentRequest request, CancellationToken cancellationToken)
    {
        var created = await _shipments.CreateShipmentV2Async(request, cancellationToken);
        return CreatedAtRoute(nameof(GetShipmentV2), new { version = "2", trackingNumber = created.TrackingNumber }, created);
    }

    // ---------------------------------------------------------------- update

    /// <summary>Update the editable details of a shipment (v1). Only allowed while the status is Created.</summary>
    /// <param name="trackingNumber">Tracking number in format APX-YYYYMMDD.</param>
    /// <param name="request">The fields to change.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPut(TrackingNumberRoute)]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<ShipmentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShipmentDetail>> UpdateShipmentV1(
        string trackingNumber, [FromBody] UpdateShipmentRequest request, CancellationToken cancellationToken)
        => await _shipments.UpdateShipmentAsync(trackingNumber, request, cancellationToken);

    /// <summary>Update the editable details of a shipment (v2). Only allowed while the status is Created.</summary>
    /// <param name="trackingNumber">Tracking number in format APX-YYYYMMDD.</param>
    /// <param name="request">The fields to change.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPut(TrackingNumberRoute)]
    [MapToApiVersion("2.0")]
    [ProducesResponseType<V2ShipmentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<V2ShipmentDetail>> UpdateShipmentV2(
        string trackingNumber, [FromBody] UpdateShipmentRequest request, CancellationToken cancellationToken)
        => await _shipments.UpdateShipmentV2Async(trackingNumber, request, cancellationToken);

    // ---------------------------------------------------------------- status

    /// <summary>
    /// Change the status of a shipment (v1 and v2). Allowed transitions:
    /// Created → PickedUp → InTransit → OutForDelivery → Delivered, and Failed from any non-final state.
    /// Requires the Courier role.
    /// </summary>
    /// <param name="trackingNumber">Tracking number in format APX-YYYYMMDD.</param>
    /// <param name="request">The new status. Delivered needs a reason and a recipient signature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPatch(TrackingNumberRoute + "/status")]
    [Authorize(Policy = "CourierOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateShipmentStatus(
        string trackingNumber, [FromBody] UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        await _shipments.UpdateShipmentStatusAsync(trackingNumber, request, cancellationToken);
        return NoContent();
    }

    // ---------------------------------------------------------------- delete

    /// <summary>Delete a shipment (v1 and v2). Only allowed while the status is Created. Requires the Admin role.</summary>
    /// <param name="trackingNumber">Tracking number in format APX-YYYYMMDD.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpDelete(TrackingNumberRoute)]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteShipment(string trackingNumber, CancellationToken cancellationToken)
    {
        await _shipments.DeleteShipmentAsync(trackingNumber, cancellationToken);
        return NoContent();
    }
}
