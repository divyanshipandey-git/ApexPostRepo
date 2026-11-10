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
/// Scan history of a shipment and bulk upload of scans captured offline by couriers.
/// </summary>
[ApiController]
[ApiVersion("1.0", Deprecated = true)]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/shipments/{trackingNumber:" + TrackingNumberRouteConstraint.Name + "}/scans")]
[Authorize]
[EnableRateLimiting("strict")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class ScansController : ControllerBase
{
    private readonly IScanUploadService _uploads;
    private readonly IShipmentApiService _shipments;

    public ScansController(IScanUploadService uploads, IShipmentApiService shipments)
    {
        _uploads = uploads ?? throw new ArgumentNullException(nameof(uploads));
        _shipments = shipments ?? throw new ArgumentNullException(nameof(shipments));
    }

    /// <summary>
    /// Bulk upload offline scans (v1 and v2). Idempotent: scan IDs that are already stored are reported in
    /// <c>duplicateScanIds</c> and skipped; scans that break a business rule are listed in
    /// <c>rejectionDetails</c> with a reason. Requires the Courier role, and every scan must carry the
    /// courierId of the caller's token.
    /// </summary>
    /// <param name="trackingNumber">Tracking number in format APX-YYYYMMDD.</param>
    /// <param name="request">Up to 100 scans.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "CourierOnly")]
    [Consumes("application/json")]
    [ProducesResponseType<ScanUploadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ScanUploadResponse>> UploadScans(
        string trackingNumber, [FromBody] ScanUploadRequest request, CancellationToken cancellationToken)
        => await _uploads.UploadScansAsync(trackingNumber, User.FindFirst("courierId")?.Value, request, cancellationToken);

    /// <summary>Get the scan history of a shipment, ordered by scan time (v1).</summary>
    /// <param name="trackingNumber">Tracking number in format APX-YYYYMMDD.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<ScanSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ScanSummary>>> GetScanHistoryV1(
        string trackingNumber, CancellationToken cancellationToken)
        => Ok(await _shipments.GetScanHistoryAsync(trackingNumber, cancellationToken));

    /// <summary>Get the scan history of a shipment, ordered by scan time (v2).</summary>
    /// <param name="trackingNumber">Tracking number in format APX-YYYYMMDD.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [MapToApiVersion("2.0")]
    [ProducesResponseType<IReadOnlyList<V2ScanSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<V2ScanSummary>>> GetScanHistoryV2(
        string trackingNumber, CancellationToken cancellationToken)
        => Ok(await _shipments.GetScanHistoryV2Async(trackingNumber, cancellationToken));
}
