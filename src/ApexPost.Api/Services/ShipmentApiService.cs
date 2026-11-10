using System.Text.Json;
using ApexPost.Api.DTOs;
using V2DTOs = ApexPost.Api.DTOs.v2;
using ApexPost.Api.Exceptions;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DomainStatus = ApexPost.Domain.Entities.ShipmentStatus;

namespace ApexPost.Api.Services;

/// <summary>
/// Service for shipment API operations.
/// </summary>
public interface IShipmentApiService
{
    // v1
    Task<PagedResponse<ShipmentSummary>> GetShipmentsAsync(ShipmentFilter filter, CancellationToken cancellationToken = default);
    Task<ShipmentDetail> GetShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScanSummary>> GetScanHistoryAsync(string trackingNumber, CancellationToken cancellationToken = default);
    Task<ShipmentDetail> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken cancellationToken = default);
    Task<ShipmentDetail> UpdateShipmentAsync(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken = default);

    // shared by both versions (no response body)
    Task UpdateShipmentStatusAsync(string trackingNumber, UpdateStatusRequest request, CancellationToken cancellationToken = default);
    Task DeleteShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default);

    // v2 (nested recipient + latestScan)
    Task<V2DTOs.V2PagedResponse<V2DTOs.V2ShipmentSummary>> GetShipmentsV2Async(ShipmentFilter filter, CancellationToken cancellationToken = default);
    Task<V2DTOs.V2ShipmentDetail> GetShipmentV2Async(string trackingNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<V2DTOs.V2ScanSummary>> GetScanHistoryV2Async(string trackingNumber, CancellationToken cancellationToken = default);
    Task<V2DTOs.V2ShipmentDetail> CreateShipmentV2Async(CreateShipmentRequest request, CancellationToken cancellationToken = default);
    Task<V2DTOs.V2ShipmentDetail> UpdateShipmentV2Async(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken = default);
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

        var query = ApplyFilter(_shipmentRepository.Query(), filter);
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
                LastScanAtUtc = s.Scans.OrderByDescending(scan => scan.ScannedAtUtc).Select(scan => (DateTimeOffset?)scan.ScannedAtUtc).FirstOrDefault(),
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

    public async Task<ShipmentDetail> GetShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var shipment = await RequireShipmentAsync(trackingNumber, cancellationToken);
        return MapToDetail(shipment);
    }

    public async Task<IReadOnlyList<ScanSummary>> GetScanHistoryAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var shipment = await RequireShipmentAsync(trackingNumber, cancellationToken);
        return MapToDetail(shipment).Scans;
    }

    public async Task<ShipmentDetail> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await CreateCoreAsync(request, cancellationToken);
        return MapToDetail(shipment);
    }

    public async Task<ShipmentDetail> UpdateShipmentAsync(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await UpdateCoreAsync(trackingNumber, request, cancellationToken);
        return MapToDetail(shipment);
    }

    public async Task UpdateShipmentStatusAsync(string trackingNumber, UpdateStatusRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await RequireShipmentAsync(trackingNumber, cancellationToken);
        var previousStatus = shipment.Status;
        var newStatus = (DomainStatus)request.Status;

        if (!IsValidTransition(previousStatus, newStatus))
        {
            throw new ConflictException(
                $"Invalid status transition from {previousStatus} to {request.Status}. " +
                $"Allowed: {string.Join(", ", GetAllowedTransitions(previousStatus))}");
        }

        shipment.UpdateStatus(
            newStatus: newStatus,
            reason: request.Reason,
            recipientSignature: request.RecipientSignature,
            latitude: request.Location?.Latitude,
            longitude: request.Location?.Longitude);

        await _shipmentRepository.UpdateAsync(shipment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated shipment {TrackingNumber} status from {PreviousStatus} to {NewStatus}",
            trackingNumber, previousStatus, request.Status);
    }

    public async Task DeleteShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var shipment = await RequireShipmentAsync(trackingNumber, cancellationToken);

        if (shipment.Status != DomainStatus.Created)
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

        var query = ApplyFilter(_shipmentRepository.Query(), filter);
        var totalCount = await query.CountAsync(cancellationToken);

        // Materialise the page first: the nested recipient / latestScan mapping cannot be translated to SQL.
        var shipments = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new V2DTOs.V2PagedResponse<V2DTOs.V2ShipmentSummary>
        {
            Items = shipments.Select(MapToV2Summary).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<V2DTOs.V2ShipmentDetail> GetShipmentV2Async(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var shipment = await RequireShipmentAsync(trackingNumber, cancellationToken);
        return MapToV2Detail(shipment);
    }

    public async Task<IReadOnlyList<V2DTOs.V2ScanSummary>> GetScanHistoryV2Async(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var shipment = await RequireShipmentAsync(trackingNumber, cancellationToken);
        return MapToV2Detail(shipment).Scans;
    }

    public async Task<V2DTOs.V2ShipmentDetail> CreateShipmentV2Async(CreateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await CreateCoreAsync(request, cancellationToken);
        return MapToV2Detail(shipment);
    }

    public async Task<V2DTOs.V2ShipmentDetail> UpdateShipmentV2Async(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await UpdateCoreAsync(trackingNumber, request, cancellationToken);
        return MapToV2Detail(shipment);
    }

    // =========================================================================
    // Shared core logic
    // =========================================================================

    private async Task<TrackedShipment> RequireShipmentAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        return await _shipmentRepository.GetByTrackingNumberAsync(trackingNumber, cancellationToken)
            ?? throw new NotFoundException($"Shipment {trackingNumber} not found");
    }

    private async Task<TrackedShipment> CreateCoreAsync(CreateShipmentRequest request, CancellationToken cancellationToken)
    {
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

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueKeyConflictException)
        {
            // Two requests created the same tracking number at the same time; the unique index decided.
            throw new ConflictException($"Shipment with tracking number {request.TrackingNumber} already exists");
        }

        _logger.LogInformation("Created shipment {TrackingNumber}", shipment.TrackingNumber);
        return shipment;
    }

    private async Task<TrackedShipment> UpdateCoreAsync(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken)
    {
        var shipment = await RequireShipmentAsync(trackingNumber, cancellationToken);

        if (shipment.Status != DomainStatus.Created)
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
        return shipment;
    }

    private static IQueryable<TrackedShipment> ApplyFilter(IQueryable<TrackedShipment> query, ShipmentFilter filter)
    {
        if (filter.Status.HasValue)
        {
            var status = (DomainStatus)filter.Status.Value;
            query = query.Where(s => s.Status == status);
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

        return query;
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

    private static ShipmentDetail MapToDetail(TrackedShipment shipment)
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

    private static V2DTOs.V2ShipmentSummary MapToV2Summary(TrackedShipment s)
    {
        var latestScan = s.Scans.OrderByDescending(scan => scan.ScannedAtUtc).FirstOrDefault();
        
        return new V2DTOs.V2ShipmentSummary
        {
            TrackingNumber = s.TrackingNumber,
            Status = (ApexPost.Api.DTOs.ShipmentStatus)s.Status,
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
            LastScanAtUtc = latestScan?.ScannedAtUtc,
            ScanCount = s.Scans.Count,
            LatestScan = latestScan != null ? new V2DTOs.V2LatestScanSummary
            {
                ScanType = (ApexPost.Api.DTOs.ScanType)latestScan.ScanType,
                Location = latestScan.Location,
                ScannedAtUtc = latestScan.ScannedAtUtc
            } : null
        };
    }

    private static V2DTOs.V2ShipmentDetail MapToV2Detail(TrackedShipment shipment)
    {
        var latestScan = shipment.Scans.OrderByDescending(s => s.ScannedAtUtc).FirstOrDefault();

        return new V2DTOs.V2ShipmentDetail
        {
            TrackingNumber = shipment.TrackingNumber,
            Status = (ApexPost.Api.DTOs.ShipmentStatus)shipment.Status,
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
            LatestScan = latestScan is null
                ? null
                : new V2DTOs.V2LatestScanSummary
                {
                    ScanType = (ApexPost.Api.DTOs.ScanType)latestScan.ScanType,
                    Location = latestScan.Location,
                    ScannedAtUtc = latestScan.ScannedAtUtc
                },
            Scans = shipment.Scans
                .OrderBy(s => s.ScannedAtUtc)
                .Select(s => new V2DTOs.V2ScanSummary
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
}