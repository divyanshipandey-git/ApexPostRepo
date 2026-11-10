using ApexPost.Api.DTOs;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Domain.Entities;
using DomainEntities = ApexPost.Domain.Entities;
using ApexPost.Api.Exceptions;

namespace ApexPost.Api.Services;

/// <summary>
/// Service for handling bulk scan uploads from courier devices.
/// </summary>
public interface IScanUploadService
{
    /// <summary>
    /// Stores the scans of one courier for a shipment. Idempotent on scan ID.
    /// </summary>
    /// <param name="trackingNumber">Shipment the scans belong to.</param>
    /// <param name="callerCourierId">The courierId claim of the authenticated caller.</param>
    /// <param name="request">The scans to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ForbiddenException">The caller has no courierId claim, or a scan belongs to another courier.</exception>
    /// <exception cref="NotFoundException">The shipment does not exist.</exception>
    /// <exception cref="ConflictException">The same scan IDs were uploaded concurrently; the client should retry.</exception>
    Task<ScanUploadResponse> UploadScansAsync(string trackingNumber, string? callerCourierId, ScanUploadRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of scan upload service with idempotent processing.
/// </summary>
public sealed class ScanUploadService : IScanUploadService
{
    private readonly ITrackedShipmentRepository _shipmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ScanUploadService> _logger;

    public ScanUploadService(
        ITrackedShipmentRepository shipmentRepository,
        IUnitOfWork unitOfWork,
        ILogger<ScanUploadService> logger)
    {
        _shipmentRepository = shipmentRepository ?? throw new ArgumentNullException(nameof(shipmentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ScanUploadResponse> UploadScansAsync(string trackingNumber, string? callerCourierId, ScanUploadRequest request, CancellationToken cancellationToken = default)
    {
        // Ownership: a courier may only upload their own scans.
        if (string.IsNullOrWhiteSpace(callerCourierId))
        {
            throw new ForbiddenException("Missing courierId claim in token");
        }

        var foreignScanIds = request.Scans
            .Where(s => !string.Equals(s.CourierId, callerCourierId, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.ScanId)
            .ToList();

        if (foreignScanIds.Count > 0)
        {
            throw new ForbiddenException(
                $"Scans {string.Join(", ", foreignScanIds)} have a courierId that does not match your token");
        }

        var shipment = await _shipmentRepository.GetByTrackingNumberWithTrackingAsync(trackingNumber, cancellationToken)
            ?? throw new NotFoundException($"Shipment {trackingNumber} not found");

        var response = new ScanUploadResponse();
        var scanIdsInRequest = request.Scans.Select(s => s.ScanId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Idempotency: scan IDs that are already stored (any shipment) are duplicates.
        var existingScans = await _shipmentRepository.GetScansByIdsAsync(scanIdsInRequest, cancellationToken);
        var knownScanIds = existingScans.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var scanRequest in request.Scans)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (knownScanIds.Contains(scanRequest.ScanId))
            {
                // A duplicate is not an error: it is counted and listed, but never reported as a rejection.
                response.DuplicatesSkipped++;
                response.DuplicateScanIds.Add(scanRequest.ScanId);
                continue;
            }

            var (isValid, errorMessage) = ValidateScanBusinessRules(scanRequest, shipment);
            if (!isValid)
            {
                response.Rejected++;
                response.RejectionDetails.Add(new ScanRejectionDetail
                {
                    ScanId = scanRequest.ScanId,
                    Reason = errorMessage!
                });
                continue;
            }

            var scan = new TrackedShipmentScan
            {
                Id = scanRequest.ScanId,
                TrackedShipmentId = shipment.Id,
                ScanType = (DomainEntities.ScanType)scanRequest.ScanType,
                CourierId = scanRequest.CourierId,
                Location = scanRequest.Location,
                ScannedAtUtc = scanRequest.ScannedAtUtc,
                Notes = scanRequest.Notes,
                Latitude = scanRequest.GpsCoordinates?.Latitude,
                Longitude = scanRequest.GpsCoordinates?.Longitude,
                RecipientSignature = scanRequest.RecipientSignature
            };

            shipment.AddScan(scan);
            knownScanIds.Add(scanRequest.ScanId); // also de-duplicates inside one batch
            response.Accepted++;
        }

        if (response.Accepted > 0)
        {
            await _shipmentRepository.UpdateAsync(shipment, cancellationToken);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueKeyConflictException ex)
            {
                // The primary key on TrackedShipmentScans.Id is the hard guarantee that a scan ID is stored once.
                // If another request stored one of these IDs between our check and our save, nothing is
                // half-saved (single SaveChanges); ask the client to retry, and the retry reports duplicates.
                _logger.LogWarning(ex, "Concurrent upload of the same scan IDs for {TrackingNumber}", trackingNumber);
                throw new ConflictException(
                    "One or more of these scans were uploaded concurrently by another request. Retry the upload; already stored scans are reported as duplicates.");
            }
        }

        _logger.LogInformation(
            "Scan upload for {TrackingNumber}: Accepted={Accepted}, DuplicatesSkipped={DuplicatesSkipped}, Rejected={Rejected}",
            trackingNumber, response.Accepted, response.DuplicatesSkipped, response.Rejected);

        return response;
    }

    private static (bool IsValid, string? ErrorMessage) ValidateScanBusinessRules(ScanDocumentRequest scanRequest, TrackedShipment shipment)
    {
        // Scan time cannot be in the future (5 minutes of clock skew allowed)
        if (scanRequest.ScannedAtUtc > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return (false, "Scan time cannot be in the future");
        }

        // Scan time cannot be before shipment creation
        if (scanRequest.ScannedAtUtc < shipment.CreatedAtUtc.AddMinutes(-5))
        {
            return (false, "Scan time cannot be before shipment creation");
        }

        // Status-based validation
        if ((DomainEntities.ScanType)scanRequest.ScanType == DomainEntities.ScanType.Delivered && string.IsNullOrWhiteSpace(scanRequest.RecipientSignature))
        {
            return (false, "Recipient signature is required for Delivered scans");
        }

        if ((DomainEntities.ScanType)scanRequest.ScanType == DomainEntities.ScanType.Failed && string.IsNullOrWhiteSpace(scanRequest.Notes))
        {
            return (false, "Reason is required for Failed scans");
        }

        // Validate sequence - simplified check
        var existingScans = shipment.Scans.OrderBy(s => s.ScannedAtUtc).ToList();
        if (existingScans.Count > 0)
        {
            var lastScan = existingScans.Last();
            if (scanRequest.ScannedAtUtc < lastScan.ScannedAtUtc.AddMinutes(-5))
            {
                return (false, "Scan time cannot be before previous scan");
            }

            // Basic state machine validation
            var isValidTransition = IsValidScanTransition((DomainEntities.ScanType)lastScan.ScanType, (DomainEntities.ScanType)scanRequest.ScanType);
            if (!isValidTransition)
            {
                return (false, $"Invalid scan sequence: {lastScan.ScanType} → {scanRequest.ScanType}");
            }
        }

        return (true, null);
    }

    private static bool IsValidScanTransition(DomainEntities.ScanType from, DomainEntities.ScanType to)
    {
        return (from, to) switch
        {
            (ApexPost.Domain.Entities.ScanType.Pickup, ApexPost.Domain.Entities.ScanType.InTransit) => true,
            (ApexPost.Domain.Entities.ScanType.Pickup, ApexPost.Domain.Entities.ScanType.Delivered) => true, // Direct delivery
            (ApexPost.Domain.Entities.ScanType.InTransit, ApexPost.Domain.Entities.ScanType.InTransit) => true, // Multiple in-transit
            (ApexPost.Domain.Entities.ScanType.InTransit, ApexPost.Domain.Entities.ScanType.Delivered) => true,
            (ApexPost.Domain.Entities.ScanType.InTransit, ApexPost.Domain.Entities.ScanType.Failed) => true,
            (ApexPost.Domain.Entities.ScanType.Delivered, _) => false, // Terminal state
            (ApexPost.Domain.Entities.ScanType.Failed, _) => false,   // Terminal state
            _ => true // Allow pickup as first scan
        };
    }
}