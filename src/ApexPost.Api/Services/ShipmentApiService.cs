using System.Text.Json;
using ApexPost.Api.DTOs;
using V2DTOs = ApexPost.Api.DTOs.v2;
using ApexPost.Api.Exceptions;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Api.Services;

/// <summary>
/// Service for shipment API operations.
/// </summary>
public interface IShipmentApiService
{
    Task<PagedResponse<ShipmentSummary>> GetShipmentsAsync(ShipmentFilter filter, CancellationToken cancellationToken = default);
    Task<ShipmentDetail?> GetShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default);
    Task<ShipmentDetail> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken cancellationToken = default);
    Task<ShipmentDetail> UpdateShipmentAsync(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken = default);
    Task UpdateShipmentStatusAsync(string trackingNumber, UpdateStatusRequest request, CancellationToken cancellationToken = default);
    Task DeleteShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default);

    // v2 methods
    Task<V2DTOs.V2PagedResponse<V2DTOs.V2ShipmentSummary>> GetShipmentsV2Async(ShipmentFilter filter, CancellationToken cancellationToken = default);
    Task<V2DTOs.V2ShipmentDetail?> GetShipmentV2Async(string trackingNumber, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of shipment API service.
/// </summary>
public sealed class ShipmentApiService : IShipmentApiService
{
    private readonly ITrackedShipmentRepository _shipmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShipmentApiService> _logger;

    public ShipmentApiService(
        ITrackedShipmentRepository shipmentRepository,
        IUnitOfWork unitOfWork,
        ILogger<ShipmentApiService> logger)
    {
        _shipmentRepository = shipmentRepository ?? throw new ArgumentNullException(nameof(shipmentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // =========================================================================
    // v1 Methods
    // =========================================================================

    public async Task<PagedResponse<ShipmentSummary>> GetShipmentsAsync(ShipmentFilter filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, ShipmentFilter.MaxPageSize);

        var query = _shipmentRepository.Query();

        if (filter.Status.HasValue)
        {
            query = query.Where(s => s.Status == (ApexPost.Domain.Entities.ShipmentStatus)filter.Status.Value);
        }

        if (!string.IsNullOrEmpty(filter.City))
        {
            query = query.Where(s => s.RecipientCity.Contains(filter.City));
        }

        if (!string.IsNullOrEmpty(filter.CourierId))
        {
            query = query.Where(s => s.Scans.Any(scan => scan.CourierId == filter.CourierId));
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(s => s.CreatedAtUtc >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(s => s.CreatedAtUtc <= filter.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ShipmentSummary
            {
                TrackingNumber = s.TrackingNumber,
                Status = (ApexPost.Api.DTOs.ShipmentStatus)s.Status,
                RecipientName = s.RecipientName,
                RecipientCity = s.RecipientCity,
                WeightKg = s.WeightKg,
                CreatedAtUtc = s.CreatedAtUtc,
                LastScanAtUtc = s.Scans.OrderByDescending(scan => scan.ScannedAtUtc).Select(scan => scan.ScannedAtUtc).FirstOrDefault(),
                ScanCount = s.Scans.Count
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<ShipmentSummary>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ShipmentDetail?> GetShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var shipment = await _shipmentRepository.GetByTrackingNumberAsync(trackingNumber, cancellationToken);
        if (shipment == null) return null;

        return MapToDetail(shipment);
    }

    public async Task<ShipmentDetail> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        // Check for duplicate tracking number
        var existing = await _shipmentRepository.GetByTrackingNumberAsync(request.TrackingNumber, cancellationToken);
        if (existing != null)
        {
            throw new ConflictException($"Shipment with tracking number {request.TrackingNumber} already exists");
        }

        var shipment = TrackedShipment.Create(
            trackingNumber: request.TrackingNumber,
            weightKg: request.WeightKg,
            recipientName: request.RecipientName,
            recipientPhone: request.RecipientPhone,
            recipientAddress: request.RecipientAddress,
            recipientCity: request.RecipientCity,
            recipientPostalCode: request.RecipientPostalCode,
            recipientCountry: request.RecipientCountry ?? "US",
            specialInstructions: request.SpecialInstructions,
            declaredValue: request.DeclaredValue,
            createdAtUtc: DateTimeOffset.UtcNow);

        await _shipmentRepository.AddAsync(shipment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created shipment {TrackingNumber}", shipment.TrackingNumber);

        return MapToDetail(shipment);
    }

    public async Task<ShipmentDetail> UpdateShipmentAsync(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await _shipmentRepository.GetByTrackingNumberAsync(trackingNumber, cancellationToken);
        if (shipment == null)
        {
            throw new NotFoundException($"Shipment {trackingNumber} not found");
        }

        // Only allow updates while in Created status
        if (shipment.Status != ApexPost.Domain.Entities.ShipmentStatus.Created)
        {
            throw new ConflictException("Shipment can only be updated while in Created status");
        }

        shipment.UpdateDetails(
            weightKg: request.WeightKg,
            recipientName: request.RecipientName,
            recipientPhone: request.RecipientPhone,
            recipientAddress: request.RecipientAddress,
            recipientCity: request.RecipientCity,
            recipientPostalCode: request.RecipientPostalCode,
            recipientCountry: request.RecipientCountry,
            specialInstructions: request.SpecialInstructions,
            declaredValue: request.DeclaredValue);

        await _shipmentRepository.UpdateAsync(shipment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated shipment {TrackingNumber}", shipment.TrackingNumber);

        return MapToDetail(shipment);
    }

    public async Task UpdateShipmentStatusAsync(string trackingNumber, UpdateStatusRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await _shipmentRepository.GetByTrackingNumberAsync(trackingNumber, cancellationToken);
        if (shipment == null)
        {
            throw new NotFoundException($"Shipment {trackingNumber} not found");
        }

        // Validate status transition
        if (!IsValidTransition(shipment.Status, (ApexPost.Domain.Entities.ShipmentStatus)request.Status))
        {
            throw new ConflictException(
                $"Invalid status transition from {shipment.Status} to {request.Status}. " +
                $"Allowed: {string.Join(", ", GetAllowedTransitions(shipment.Status))}");
        }

        // Validate Delivered requires signature
        if (request.Status == ApexPost.Api.DTOs.ShipmentStatus.Delivered && string.IsNullOrWhiteSpace(request.RecipientSignature))
        {
            throw new ConflictException("Recipient signature is required for Delivered status");
        }

        var latitude = request.Location?.Latitude;
        var longitude = request.Location?.Longitude;

        shipment.UpdateStatus(
            newStatus: (ApexPost.Domain.Entities.ShipmentStatus)request.Status,
            reason: request.Reason,
            recipientSignature: request.RecipientSignature,
            latitude: latitude,
            longitude: longitude);

        await _shipmentRepository.UpdateAsync(shipment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated shipment {TrackingNumber} status from {PreviousStatus} to {NewStatus}",
            trackingNumber, shipment.Status, request.Status);
    }

    public async Task DeleteShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var shipment = await _shipmentRepository.GetByTrackingNumberAsync(trackingNumber, cancellationToken);
        if (shipment == null)
        {
            throw new NotFoundException($"Shipment {trackingNumber} not found");
        }

        if (shipment.Status != ApexPost.Domain.Entities.ShipmentStatus.Created)
        {
            throw new ConflictException("Shipment can only be deleted while in Created status");
        }

        await _shipmentRepository.DeleteAsync(shipment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted shipment {TrackingNumber}", trackingNumber);
    }

    // =========================================================================
    // v2 Methods
    // =========================================================================

    public async Task<V2DTOs.V2PagedResponse<V2DTOs.V2ShipmentSummary>> GetShipmentsV2Async(ShipmentFilter filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, ShipmentFilter.MaxPageSize);

        var query = _shipmentRepository.Query();

        if (filter.Status.HasValue)
        {
            query = query.Where(s => s.Status == (ApexPost.Domain.Entities.ShipmentStatus)filter.Status.Value);
        }

        if (!string.IsNullOrEmpty(filter.City))
        {
            query = query.Where(s => s.RecipientCity.Contains(filter.City));
        }

        if (!string.IsNullOrEmpty(filter.CourierId))
        {
            query = query.Where(s => s.Scans.Any(scan => scan.CourierId == filter.CourierId));
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(s => s.CreatedAtUtc >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(s => s.CreatedAtUtc <= filter.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => MapToV2Summary(s))
            .ToListAsync(cancellationToken);

        return new V2DTOs.V2PagedResponse<V2DTOs.V2ShipmentSummary>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<V2DTOs.V2ShipmentDetail?> GetShipmentV2Async(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var shipment = await _shipmentRepository.GetByTrackingNumberAsync(trackingNumber, cancellationToken);
        if (shipment == null) return null;

        return MapToV2Detail(shipment);
    }

    // =========================================================================
    // Mapping helpers
    // =========================================================================

    private static bool IsValidTransition(ApexPost.Domain.Entities.ShipmentStatus from, ApexPost.Domain.Entities.ShipmentStatus to)
    {
        var allowed = GetAllowedTransitions(from);
        return allowed.Contains(to);
    }

    private static IReadOnlyList<ApexPost.Domain.Entities.ShipmentStatus> GetAllowedTransitions(ApexPost.Domain.Entities.ShipmentStatus status)
    {
        return status switch
        {
            ApexPost.Domain.Entities.ShipmentStatus.Created => [ApexPost.Domain.Entities.ShipmentStatus.PickedUp, ApexPost.Domain.Entities.ShipmentStatus.Failed],
            ApexPost.Domain.Entities.ShipmentStatus.PickedUp => [ApexPost.Domain.Entities.ShipmentStatus.InTransit, ApexPost.Domain.Entities.ShipmentStatus.Failed],
            ApexPost.Domain.Entities.ShipmentStatus.InTransit => [ApexPost.Domain.Entities.ShipmentStatus.OutForDelivery, ApexPost.Domain.Entities.ShipmentStatus.Delivered, ApexPost.Domain.Entities.ShipmentStatus.Failed],
            ApexPost.Domain.Entities.ShipmentStatus.OutForDelivery => [ApexPost.Domain.Entities.ShipmentStatus.Delivered, ApexPost.Domain.Entities.ShipmentStatus.Failed],
            ApexPost.Domain.Entities.ShipmentStatus.Delivered => [],
            ApexPost.Domain.Entities.ShipmentStatus.Failed => [],
            _ => []
        };
    }

    private ShipmentDetail MapToDetail(TrackedShipment shipment)
    {
        return new ShipmentDetail
        {
            TrackingNumber = shipment.TrackingNumber,
            Status = (ApexPost.Api.DTOs.ShipmentStatus)shipment.Status,
            WeightKg = shipment.WeightKg,
            RecipientName = shipment.RecipientName,
            RecipientPhone = shipment.RecipientPhone,
            RecipientAddress = shipment.RecipientAddress,
            RecipientCity = shipment.RecipientCity,
            RecipientPostalCode = shipment.RecipientPostalCode,
            RecipientCountry = shipment.RecipientCountry,
            SpecialInstructions = shipment.SpecialInstructions,
            DeclaredValue = shipment.DeclaredValue,
            CreatedAtUtc = shipment.CreatedAtUtc,
            UpdatedAtUtc = shipment.UpdatedAtUtc,
            Scans = shipment.Scans
                .OrderBy(s => s.ScannedAtUtc)
                .Select(s => new ScanSummary
                {
                    ScanId = s.Id,
                    ScanType = (ApexPost.Api.DTOs.ScanType)s.ScanType,
                    CourierId = s.CourierId,
                    Location = s.Location,
                    ScannedAtUtc = s.ScannedAtUtc,
                    Notes = s.Notes,
                    GpsCoordinates = s.Latitude.HasValue && s.Longitude.HasValue
                        ? new ApexPost.Api.DTOs.GpsCoordinates { Latitude = s.Latitude.Value, Longitude = s.Longitude.Value }
                        : null,
                    RecipientSignature = s.RecipientSignature
                })
                .ToList()
        };
    }

    // =========================================================================
    // v2 Mapping helpers
    // =========================================================================

    private V2DTOs.V2ShipmentSummary MapToV2Summary(TrackedShipment s)
    {
        var latestScan = s.Scans.OrderByDescending(scan => scan.ScannedAtUtc).FirstOrDefault();
        
        return new V2DTOs.V2ShipmentSummary
        {
            TrackingNumber = s.TrackingNumber,
            Status = (ApexPost.Api.DTOs.v2.ShipmentStatus)s.Status,
            Recipient = new V2DTOs.V2RecipientInfo
            {
                Name = s.RecipientName,
                Phone = s.RecipientPhone,
                Address = s.RecipientAddress,
                City = s.RecipientCity,
                PostalCode = s.RecipientPostalCode,
                Country = s.RecipientCountry ?? "US"
            },
            WeightKg = s.WeightKg,
            CreatedAtUtc = s.CreatedAtUtc,
            LastScanAtUtc = s.Scans.OrderByDescending(scan => scan.ScannedAtUtc).Select(scan => scan.ScannedAtUtc).FirstOrDefault(),
            ScanCount = s.Scans.Count,
            LatestScan = latestScan != null ? new V2DTOs.V2LatestScanSummary
            {
                ScanType = (ApexPost.Api.DTOs.v2.ScanType)latestScan.ScanType,
                Location = latestScan.Location,
                ScannedAtUtc = latestScan.ScannedAtUtc
            } : null
        };
    }

    private V2DTOs.V2ShipmentDetail MapToV2Detail(TrackedShipment shipment)
    {
        return new V2DTOs.V2ShipmentDetail
        {
            TrackingNumber = shipment.TrackingNumber,
            Status = (ApexPost.Api.DTOs.v2.ShipmentStatus)shipment.Status,
            WeightKg = shipment.WeightKg,
            Recipient = new V2DTOs.V2RecipientInfo
            {
                Name = shipment.RecipientName,
                Phone = shipment.RecipientPhone,
                Address = shipment.RecipientAddress,
                City = shipment.RecipientCity,
                PostalCode = shipment.RecipientPostalCode,
                Country = shipment.RecipientCountry ?? "US"
            },
            SpecialInstructions = shipment.SpecialInstructions,
            DeclaredValue = shipment.DeclaredValue,
            CreatedAtUtc = shipment.CreatedAtUtc,
            UpdatedAtUtc = shipment.UpdatedAtUtc,
            LatestScan = shipment.Scans.OrderByDescending(s => s.ScannedAtUtc).FirstOrDefault() != null
                ? new V2DTOs.V2LatestScanSummary
                {
                    ScanType = (ApexPost.Api.DTOs.v2.ScanType)shipment.Scans.OrderByDescending(s => s.ScannedAtUtc).First().ScanType,
                    Location = shipment.Scans.OrderByDescending(s => s.ScannedAtUtc).First().Location,
                    ScannedAtUtc = shipment.Scans.OrderByDescending(s => s.ScannedAtUtc).First().ScannedAtUtc
                }
                : null,
            Scans = shipment.Scans
                .OrderBy(s => s.ScannedAtUtc)
                .Select(s => new V2DTOs.V2ScanSummary
                {
                    ScanId = s.Id,
                    ScanType = (ApexPost.Api.DTOs.v2.ScanType)s.ScanType,
                    CourierId = s.CourierId,
                    Location = s.Location,
                    ScannedAtUtc = s.ScannedAtUtc,
                    Notes = s.Notes,
                    GpsCoordinates = s.Latitude.HasValue && s.Longitude.HasValue
                        ? new V2DTOs.GpsCoordinates { Latitude = s.Latitude.Value, Longitude = s.Longitude.Value }
                        : null,
                    RecipientSignature = s.RecipientSignature
                })
                .ToList()
        };
    }
}