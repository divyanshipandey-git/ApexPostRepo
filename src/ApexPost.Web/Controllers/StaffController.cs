using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Staff;
using ApexPost.Domain.Entities;
using ApexPost.Web.Models.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApexPost.Web.Security;
using ApexPost.Web.Filters;

namespace ApexPost.Web.Controllers;

[Authorize(Roles = nameof(StaffRole.Administrator))]
[IpAllowlistProtected("Security:AdminAllowedIps")]
public sealed class StaffController(
    StaffService staffService,
    IBranchRepository branchRepository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var staffTask = staffService.GetAllAsync(cancellationToken);
        var branchesTask = branchRepository.GetAllAsync(cancellationToken);
        await Task.WhenAll(staffTask, branchesTask);
        return View(new StaffManagementViewModel
        {
            BranchCodes = (await branchesTask).Select(branch => branch.Code.Value).ToArray(),
            Staff = (await staffTask).OrderBy(staff => staff.Name, StringComparer.OrdinalIgnoreCase)
                .Select(staff => new StaffRowViewModel(
                    staff.Id, staff.Name, staff.Email, staff.Role, staff.HomeBranchCode.Value, staff.IsActive))
                .ToArray()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var staffTask = staffService.GetByIdAsync(id, cancellationToken);
        var branchesTask = branchRepository.GetAllAsync(cancellationToken);
        await Task.WhenAll(staffTask, branchesTask);
        var staff = await staffTask;
        if (staff is null)
        {
            return NotFound();
        }

        var model = await AddOptionsAsync(new StaffManagementViewModel
        {
            StaffId = staff.Id,
            Name = staff.Name,
            Email = staff.Email,
            Role = staff.Role,
            BranchCode = staff.HomeBranchCode.Value
        }, cancellationToken);
        return View("Index", model);
    }

    [HttpPost]
    [RedisplayInvalidModelState]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(StaffManagementViewModel model, CancellationToken cancellationToken)
    {
        var creating = string.IsNullOrWhiteSpace(model.StaffId);
        if (creating && string.IsNullOrWhiteSpace(model.InitialPassword))
        {
            ModelState.AddModelError(nameof(model.InitialPassword), "Set an initial password of at least 12 characters.");
        }

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Index", await AddOptionsAsync(model, cancellationToken));
        }

        try
        {
            if (creating)
            {
                model.StaffId = Guid.NewGuid().ToString("N");
                await staffService.CreateAsync(
                    model.StaffId, model.Name, model.Email, model.Role, model.BranchCode,
                    model.InitialPassword!, cancellationToken);
            }
            else if (!await staffService.UpdateProfileAsync(
                         model.StaffId!, model.Name, model.Email, model.Role, model.BranchCode, cancellationToken))
            {
                return NotFound();
            }
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            model.InitialPassword = null;
            return View("Index", await AddOptionsAsync(model, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            model.InitialPassword = null;
            return View("Index", await AddOptionsAsync(model, cancellationToken));
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string staffId, bool active, CancellationToken cancellationToken)
    {
        if (!await staffService.SetActiveStatusAsync(staffId, active, cancellationToken))
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<StaffManagementViewModel> AddOptionsAsync(
        StaffManagementViewModel model,
        CancellationToken cancellationToken)
    {
        model.BranchCodes = (await branchRepository.GetAllAsync(cancellationToken))
            .Select(branch => branch.Code.Value).ToArray();
        model.Staff = (await staffService.GetAllAsync(cancellationToken))
            .OrderBy(staff => staff.Name, StringComparer.OrdinalIgnoreCase)
            .Select(staff => new StaffRowViewModel(
                staff.Id, staff.Name, staff.Email, staff.Role, staff.HomeBranchCode.Value, staff.IsActive))
            .ToArray();
        return model;
    }
}
