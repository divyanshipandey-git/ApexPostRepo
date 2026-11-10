using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using ApexPost.Api.DTOs;
using ApexPost.Api.Services;
using ApexPost.Api.Exceptions;

namespace ApexPost.Api.Endpoints;

/// <summary>
/// Scan API endpoints for managing shipment scan history and bulk uploads.
/// </summary>
[Tags("Scans")]
public static class ScanEndpoints
{
    /// <summary>
    /// Maps the scan endpoints for the specified version.
    /// </summary>
    /// <param name="apiGroup">The route builder for the API version group.</param>
    /// <param name="version">The API version (v1 or v2).</param>
    public static void Map(IEndpointRouteBuilder apiGroup, string version)
    {
        var group = apiGroup.MapGroup("/shipments/{trackingNumber}/scans")
            .WithTags("Scans")
            .RequireRateLimiting("strict"); // Stricter rate limiting for uploads

        var versionSuffix = version == "1" ? "V1" : "V2";

        // POST /api/v{version}/shipments/{trackingNumber}/scans - Bulk upload scans
        // Requires Courier role and courierId claim must match the scans' courierId
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

            // Verify courierId claim matches the scans being uploaded
            var courierIdClaim = httpContext.User.FindFirst("courierId")?.Value;
            if (string.IsNullOrEmpty(courierIdClaim))
            {
                return Results.Problem(
                    detail: "Missing courierId claim in token",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    type: "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                    instance: httpContext.Request.Path);
            }

            // Check that all scans in the request belong to this courier
            var invalidCourierScans = request.Scans
                .Where(s => !string.Equals(s.CourierId, courierIdClaim, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.ScanId)
                .ToList();

            if (invalidCourierScans.Count > 0)
            {
                return Results.Problem(
                    detail: $"Scans {string.Join(", ", invalidCourierScans)} have courierId that does not match your token",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    type: "https://tools.ietf.org/html/rfc7231#section-6.5.3",
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
        .WithName($"UploadScans{versionSuffix}")
        .WithSummary("Bulk upload offline scans")
        .WithDescription("Uploads scans from courier offline devices. Idempotent: duplicate scan IDs are skipped. Returns counts of accepted, duplicates skipped, and rejected scans with reasons. Requires Courier role and matching courierId claim.")
        .Produces<ScanUploadResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization("CourierOnly");

        // GET /api/v{version}/shipments/{trackingNumber}/scans - Scan history
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
        .WithName($"GetScanHistory{versionSuffix}")
        .WithSummary("Get scan history for a shipment")
        .WithDescription("Returns all scans for the specified shipment, ordered by scan time.")
        .Produces<IReadOnlyList<ScanSummary>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization("AuthenticatedUser");
    }
}

/// <summary>
/// Extension method to check for JSON content type.
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// Checks if the request has a JSON content type.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>True if the content type is application/json, false otherwise.</returns>
    public static bool HasJsonContentType(this HttpRequest request)
    {
        if (request.ContentType is null) return false;
        return request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
    }
}