using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApexPost.Api.DTOs;
using ApexPost.Api.Services;
using ApexPost.Api.Exceptions;

namespace ApexPost.Api.Endpoints;

/// <summary>
/// Shipment API endpoints.
/// </summary>
public static class ShipmentEndpoints
{
    public static void Map(IEndpointRouteBuilder apiGroup)
    {
        var group = apiGroup.MapGroup("/shipments")
            .WithTags("Shipments")
            .RequireRateLimiting("ApiPolicy");

        // GET /api/v1/shipments - Paged, filtered list
        group.MapGet("/", async (
            [AsParameters] ShipmentFilter filter,
            IShipmentApiService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetShipmentsAsync(filter, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetShipments")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Get paged list of shipments",
            Description = "Returns a paginated, filtered list of shipments. Default page size is 20, maximum 50."
        })
        .Produces<PagedResponse<ShipmentSummary>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        // GET /api/v1/shipments/{trackingNumber} - Single shipment
        group.MapGet("/{trackingNumber}", async (
            string trackingNumber,
            IShipmentApiService service,
            CancellationToken cancellationToken) =>
        {
            var shipment = await service.GetShipmentAsync(trackingNumber, cancellationToken);
            return shipment is not null ? Results.Ok(shipment) : Results.NotFound();
        })
        .WithName("GetShipment")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Get a single shipment by tracking number",
            Description = "Returns detailed information about a shipment including all scans."
        })
        .Produces<ShipmentDetail>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/shipments - Create shipment
        group.MapPost("/", async (
            [FromBody] CreateShipmentRequest request,
            IShipmentApiService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var shipment = await service.CreateShipmentAsync(request, cancellationToken);
                return Results.CreatedAtRoute("GetShipment", new { trackingNumber = shipment.TrackingNumber }, shipment);
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
        .WithName("CreateShipment")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Create a new shipment",
            Description = "Creates a new shipment with the provided details. Tracking number must be unique and match format APX-YYYYMMDD."
        })
        .Produces<ShipmentDetail>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // PUT /api/v1/shipments/{trackingNumber} - Update shipment
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
        .WithName("UpdateShipment")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Update an existing shipment",
            Description = "Updates editable details of a shipment. Only allowed while status is Created."
        })
        .Produces<ShipmentDetail>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // PATCH /api/v1/shipments/{trackingNumber}/status - Change status only
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
        .WithName("UpdateShipmentStatus")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Update shipment status",
            Description = "Changes the status of a shipment. Allowed transitions: Created → PickedUp → InTransit → OutForDelivery → Delivered/Failed."
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // DELETE /api/v1/shipments/{trackingNumber} - Delete shipment
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
        .WithName("DeleteShipment")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Delete a shipment",
            Description = "Deletes a shipment. Only allowed while status is Created."
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }
}