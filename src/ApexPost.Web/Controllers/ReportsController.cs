using System.Security.Claims;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Reports;
using ApexPost.Application.Services.Reports;
using ApexPost.Domain.Entities;
using ApexPost.Web.Models.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class ReportsController(
    ReportingService reportingService,
    IStaffRepository staffRepository,
    TimeProvider timeProvider) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? date, CancellationToken cancellationToken)
    {
        var staff = await GetCurrentStaffAsync(cancellationToken);
        if (staff is null || staff.Role is not (StaffRole.BranchManager or StaffRole.Auditor or StaffRole.Administrator))
        {
            return Forbid();
        }

        var branchScope = staff.Role == StaffRole.BranchManager
            ? staff.HomeBranchCode.Value
            : null;
        var reportDate = date ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var sales = await reportingService.GetDailyBranchSalesReportsAsync(reportDate, branchScope, cancellationToken);
        var lowStock = await reportingService.GetLowStockReportAsync(branchScope, cancellationToken);
        var shipping = await reportingService.GetShippingVolumeReportAsync(reportDate, branchScope, cancellationToken);
        var canViewAuditorReport = staff.Role is StaffRole.Auditor or StaffRole.Administrator;
        var services = canViewAuditorReport
            ? await reportingService.GetBranchServiceActivityReportAsync(reportDate, cancellationToken: cancellationToken)
            : [];

        return View(new ReportsIndexViewModel(
            reportDate,
            branchScope,
            sales,
            lowStock,
            shipping,
            services,
            canViewAuditorReport));
    }

    private async Task<Staff?> GetCurrentStaffAsync(CancellationToken cancellationToken)
    {
        var staffId = User.FindFirstValue("StaffId");
        var claimedRole = User.FindFirstValue("Role");
        var claimedBranch = User.FindFirstValue("BranchCode");
        if (string.IsNullOrWhiteSpace(staffId) ||
            !Enum.TryParse<StaffRole>(claimedRole, out var role) ||
            !Enum.IsDefined(role) ||
            string.IsNullOrWhiteSpace(claimedBranch))
        {
            return null;
        }

        var staff = await staffRepository.GetByIdAsync(staffId, cancellationToken);
        if (staff is not { IsActive: true } || staff.Role != role ||
            !string.Equals(staff.HomeBranchCode.Value, claimedBranch, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return staff;
    }
}
