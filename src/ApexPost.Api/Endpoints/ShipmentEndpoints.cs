using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApexPost.Api.DTOs;
using ApexPost.Api.Services;
using ApexPost.Api.Exceptions;

namespace ApexPost.Api.Endpoints;

/// <summary>
/// Shipment API endpoints for managing tracked shipments.
/// </summary>
[Tags("Shipments")]
public static class ShipmentEndpoints
{
    /// <summary>
    /// Maps the shipment endpoints for the specified version.
    /// </summary>
    /// <param name="apiGroup">The route builder for the API version group.</param>
    /// <param name="version">The API version (v1 or v2).</param>
    public static void Map(IEndpointRouteBuilder apiGroup, string version)
    {
        var group = apiGroup.MapGroup("/shipments")
            .WithTags("Shipments")
            .RequireRateLimiting("ApiPolicy");

        var versionSuffix = version == "1" ? "V1" : "V2";

        // GET /api/v{version}/shipments - Paged, filtered list
        group.MapGet("/", async (
            [AsParameters] ShipmentFilter filter,
            IShipmentApiService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetShipmentsAsync(filter, cancellationToken);
            return Results.Ok(result);
        })
        .WithName($"GetShipments{versionSuffix}")
        .WithSummary("Get paged list of shipments")
        .WithDescription("Returns a paginated, filtered list of shipments. Default page size is 20, maximum 50.")
        .Produces<PagedResponse<ShipmentSummary>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .RequireAuthorization("AuthenticatedUser");

        // GET /api/v{version}/shipments/{trackingNumber} - Single shipment
        group.MapGet("/{trackingNumber}", async (
            string trackingNumber,
            IShipmentApiService service,
            CancellationToken cancellationToken) =>
        {
            var shipment = await service.GetShipmentAsync(trackingNumber, cancellationToken);
            return shipment is not null ? Results.Ok(shipment) : Results.NotFound();
        })
        .WithName($"GetShipment{versionSuffix}")
        .WithSummary("Get a single shipment by tracking number")
        .WithDescription("Returns detailed information about a shipment including all scans.")
        .Produces<ShipmentDetail>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization("AuthenticatedUser");

        // POST /api/v{version}/shipments - Create shipment
        group.MapPost("/", async (
            [FromBody] CreateShipmentRequest request,
            IShipmentApiService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var shipment = await service.CreateShipmentAsync(request, cancellationToken);
                return Results.CreatedAtRoute($"GetShipment{versionSuffix}", new { trackingNumber = shipment.TrackingNumber }, shipment);
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                    Instance = httpContext.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
                });
            }
        })
        .WithName($"CreateShipment{versionSuffix}")
        .WithSummary("Create a new shipment")
        .WithDescription("Creates a new shipment with the provided details. Tracking number must be unique and match format APX-YYYYMMDD.")
        .Produces<ShipmentDetail>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .RequireAuthorization("AuthenticatedUser");

        // PUT /api/v{version}/shipments/{trackingNumber} - Update shipment
        group.MapPut("/{trackingNumber}", async (
            string trackingNumber,
            [FromBody] UpdateShipmentRequest request,
            IShipmentApiService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var shipment = await service.UpdateShipmentAsync(trackingNumber, request, cancellationToken);
                return Results.Ok(shipment);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                    Instance = httpContext.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
                });
            }
        })
        .WithName($"UpdateShipment{versionSuffix}")
        .WithSummary("Update an existing shipment")
        .WithDescription("Updates editable details of a shipment. Only allowed while status is Created.")
        .Produces<ShipmentDetail>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .RequireAuthorization("AuthenticatedUser");

        // PATCH /api/v{version}/shipments/{trackingNumber}/status - Change status only
        group.MapPatch("/{trackingNumber}/status", async (
            string trackingNumber,
            [FromBody] UpdateStatusRequest request,
            IShipmentApiService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await service.UpdateShipmentStatusAsync(trackingNumber, request, cancellationToken);
                return Results.NoContent();
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                    Instance = httpContext.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
                });
            }
        })
        .WithName($"UpdateShipmentStatus{versionSuffix}")
        .WithSummary("Update shipment status")
        .WithDescription("Changes the status of a shipment. Allowed transitions: Created → PickedUp → InTransit → OutForDelivery → Delivered/Failed. Requires Courier role.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .RequireAuthorization("CourierOnly");

        // DELETE /api/v{version}/shipments/{trackingNumber} - Delete shipment
        group.MapDelete("/{trackingNumber}", async (
            string trackingNumber,
            IShipmentApiService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await service.DeleteShipmentAsync(trackingNumber, cancellationToken);
                return Results.NoContent();
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                    Instance = httpContext.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
                });
            }
        })
        .WithName($"DeleteShipment{versionSuffix}")
        .WithSummary("Delete a shipment")
        .WithDescription("Deletes a shipment. Only allowed while status is Created. Requires Admin role.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .RequireAuthorization("AdminOnly");
    }
}