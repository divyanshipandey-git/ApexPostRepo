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
    IBranchRepository branchRepository,
    IStaffRepository staffRepository,
    TimeProvider timeProvider) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        DateOnly? date,
        string? branchCode,
        int? threshold,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        if (threshold is < 0 or > 100_000)
        {
            ModelState.AddModelError(nameof(threshold), "Threshold must be between 0 and 100000.");
            return BadRequest(ModelState);
        }

        var staff = await GetCurrentStaffAsync(cancellationToken);
        if (staff is null || staff.Role is not (StaffRole.BranchManager or StaffRole.Auditor or StaffRole.Administrator))
        {
            return Forbid();
        }

        var branches = await branchRepository.GetAllAsync(cancellationToken);
        var branchScope = staff.Role == StaffRole.BranchManager
            ? staff.HomeBranchCode.Value
            : string.IsNullOrWhiteSpace(branchCode) ? null : branchCode.Trim().ToUpperInvariant();
        if (branchScope is not null &&
            !branches.Any(branch => string.Equals(
                branch.Code.Value, branchScope, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest("Unknown branch code.");
        }
        var visibleBranches = staff.Role == StaffRole.BranchManager
            ? branches.Where(branch => string.Equals(
                branch.Code.Value, branchScope, StringComparison.OrdinalIgnoreCase))
            : branches;

        var reportDate = date ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var sales = await reportingService.GetDailyBranchSalesReportsAsync(reportDate, branchScope, cancellationToken);
        var selectedThreshold = threshold ?? 5;
        var lowStock = await reportingService.GetLowStockReportAsync(
            branchScope, cancellationToken, selectedThreshold);
        var shipping = await reportingService.GetShippingVolumeReportAsync(reportDate, branchScope, cancellationToken);
        var canViewAuditorReport = staff.Role is StaffRole.Auditor or StaffRole.Administrator;
        var services = canViewAuditorReport
            ? await reportingService.GetBranchServiceActivityReportAsync(
                reportDate, branchScope, cancellationToken)
            : [];

        return View(new ReportsIndexViewModel(
            reportDate,
            branchScope,
            selectedThreshold,
            visibleBranches.Select(branch => new ReportBranchOption(branch.Code.Value, branch.Name)).ToArray(),
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
