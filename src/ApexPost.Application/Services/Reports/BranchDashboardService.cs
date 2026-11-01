using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Reports;
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services.Reports;

public sealed class BranchDashboardService
{
    private readonly IStaffRepository _staffRepository;
    private readonly ReportingService _reportingService;
    private readonly TimeProvider _timeProvider;

    public BranchDashboardService(
        IStaffRepository staffRepository,
        ReportingService reportingService,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(staffRepository);
        ArgumentNullException.ThrowIfNull(reportingService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _staffRepository = staffRepository;
        _reportingService = reportingService;
        _timeProvider = timeProvider;
    }

    public async Task<BranchDashboardResult?> GetAsync(
        string? staffId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(staffId))
        {
            return null;
        }

        var staff = await _staffRepository.GetByIdAsync(staffId, cancellationToken);
        if (staff is not { IsActive: true, Role: StaffRole.CounterClerk or StaffRole.BranchManager or StaffRole.Auditor or StaffRole.Administrator })
        {
            return null;
        }

        var branchCode = staff.HomeBranchCode.Value;
        var date = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        // Both report paths share the request-scoped DbContext under SQL persistence.
        // EF Core permits one active operation per context, so run these independent reads
        // sequentially rather than starting concurrent commands on the same context.
        var sales = await _reportingService.GetDailyBranchSalesAsync(branchCode, date, cancellationToken);
        var lowStock = await _reportingService.GetLowStockReportAsync(branchCode, cancellationToken);

        return new BranchDashboardResult(
            branchCode,
            date,
            sales,
            lowStock);
    }
}
