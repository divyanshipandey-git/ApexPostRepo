using ApexPost.Application.Services.Shipping;
using ApexPost.Web.Models.Shipping;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApexPost.Web.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/integrations/courier/shipments")]
[ApiKeyProtected]
[IpAllowlistProtected("Integration:AllowedIps")]
[EnableCors("PartnerIntegration")]
[EnableRateLimiting(AuthorizationPolicies.PartnerIntegrationRateLimit)]
public sealed class CourierIntegrationController(CourierStatusService courierStatusService) : ControllerBase
{
    [HttpPost("{shipmentId}/status")]
    public async Task<IActionResult> UpdateShipmentStatus(
        [FromRoute] string shipmentId,
        [FromBody] CourierStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(shipmentId) || shipmentId.Length > 128 || shipmentId.Any(char.IsControl))
        {
            return Problem(
                title: "A valid shipment identifier is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await courierStatusService.UpdateAsync(
            shipmentId,
            request.Status,
            cancellationToken);

        return result.Status switch
        {
            CourierStatusUpdateStatus.Updated => Ok(new CourierStatusUpdateResponse(
                result.ShipmentId!,
                result.CourierStatus!,
                result.ReceivedAtUtc!.Value)),
            CourierStatusUpdateStatus.ShipmentNotFound => NotFoundProblem(),
            CourierStatusUpdateStatus.InvalidShipmentId => Problem(
                title: "A valid shipment identifier is required.",
                statusCode: StatusCodes.Status400BadRequest),
            CourierStatusUpdateStatus.InvalidStatus => Problem(
                title: "Courier status must contain 1 to 100 printable characters.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private ObjectResult NotFoundProblem() => Problem(
        title: "Shipment was not found.",
        statusCode: StatusCodes.Status404NotFound);
}
