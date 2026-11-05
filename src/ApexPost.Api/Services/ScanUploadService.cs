using ApexPost.Api.DTOs;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Domain.Entities;
using DomainEntities = ApexPost.Domain.Entities;
using ApexPost.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Api.Services;

/// <summary>
/// Service for handling bulk scan uploads from courier devices.
/// </summary>
public interface IScanUploadService
{
    Task<ScanUploadResponse> UploadScansAsync(string trackingNumber, ScanUploadRequest request, CancellationToken cancellationToken = default);
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

    public async Task<ScanUploadResponse> UploadScansAsync(string trackingNumber, ScanUploadRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await _shipmentRepository.GetByTrackingNumberAsync(trackingNumber, cancellationToken);
        if (shipment == null)
        {
            throw new NotFoundException($"Shipment {trackingNumber} not found");
        }

        var response = new ScanUploadResponse();
        var scanIdsInRequest = request.Scans.Select(s => s.ScanId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Check existing scans for duplicates (idempotent)
        var existingScans = await _shipmentRepository.GetScansByIdsAsync(scanIdsInRequest, cancellationToken);
        var existingScanIds = existingScans.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var scanRequest in request.Scans)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (existingScanIds.Contains(scanRequest.ScanId))
            {
                response.DuplicatesSkipped++;
                response.RejectionDetails.Add(new ScanRejectionDetail
                {
                    ScanId = scanRequest.ScanId,
                    Reason = "Duplicate scan ID - already exists"
                });
                continue;
            }

            // Validate business rules
            var validationResult = ValidateScanBusinessRules(scanRequest, shipment);
            if (!validationResult.IsValid)
            {
                response.Rejected++;
                response.RejectionDetails.Add(new ScanRejectionDetail
                {
                    ScanId = scanRequest.ScanId,
                    Reason = validationResult.ErrorMessage!
                });
                continue;
            }

            // Create scan entity
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
            existingScanIds.Add(scanRequest.ScanId); // Prevent duplicates within same batch
            response.Accepted++;
        }

        if (response.Accepted > 0)
        {
            await _shipmentRepository.UpdateAsync(shipment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Scan upload for {TrackingNumber}: Accepted={Accepted}, DuplicatesSkipped={DuplicatesSkipped}, Rejected={Rejected}",
            trackingNumber, response.Accepted, response.DuplicatesSkipped, response.Rejected);

        return response;
    }

    private static (bool IsValid, string? ErrorMessage) ValidateScanBusinessRules(ScanDocumentRequest scanRequest, TrackedShipment shipment)
    {
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