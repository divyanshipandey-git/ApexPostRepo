using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApexPost.Api.DTOs;
using ApexPost.Api.Services;
using ApexPost.Api.Exceptions;

namespace ApexPost.Api.Endpoints;

/// <summary>
/// Scan API endpoints.
/// </summary>
public static class ScanEndpoints
{
    public static void Map(IEndpointRouteBuilder apiGroup)
    {
        var group = apiGroup.MapGroup("/shipments/{trackingNumber}/scans")
            .WithTags("Scans")
            .RequireRateLimiting("strict"); // Stricter rate limiting for uploads

        // POST /api/v1/shipments/{trackingNumber}/scans - Bulk upload scans
        group.MapPost("/", async (
            string trackingNumber,
            [FromBody] ScanUploadRequest request,
            IScanUploadService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!httpContext.Request.HasJsonContentType())
            {
                return Results.Problem(
                    detail: "Content-Type must be application/json",
                    statusCode: StatusCodes.Status415UnsupportedMediaType,
                    title: "Unsupported Media Type",
                    type: "https://tools.ietf.org/html/rfc7231#section-6.5.13",
                    instance: httpContext.Request.Path);
            }

            try
            {
                var response = await service.UploadScansAsync(trackingNumber, request, cancellationToken);
                return Results.Ok(response);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("UploadScans")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Bulk upload offline scans",
            Description = "Uploads scans from courier offline devices. Idempotent: duplicate scan IDs are skipped. Returns counts of accepted, duplicates skipped, and rejected scans with reasons."
        })
        .Produces<ScanUploadResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/shipments/{trackingNumber}/scans - Scan history
        group.MapGet("/", async (
            string trackingNumber,
            IShipmentApiService service,
            CancellationToken cancellationToken) =>
        {
            var shipment = await service.GetShipmentAsync(trackingNumber, cancellationToken);
            return shipment is not null
                ? Results.Ok(shipment.Scans)
                : Results.NotFound();
        })
        .WithName("GetScanHistory")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Get scan history for a shipment",
            Description = "Returns all scans for the specified shipment, ordered by scan time."
        })
        .Produces<IReadOnlyList<ScanSummary>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}

/// <summary>
/// Extension method to check for JSON content type.
/// </summary>
public static class HttpRequestExtensions
{
    public static bool HasJsonContentType(this HttpRequest request)
    {
        if (request.ContentType is null) return false;
        return request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
    }
}