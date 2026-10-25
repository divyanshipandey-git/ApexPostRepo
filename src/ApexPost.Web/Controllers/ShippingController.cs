using System.Security.Claims;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Roles = "CounterClerk,BranchManager,Administrator")]
public sealed class ShippingController(
    IShipmentRepository shipmentRepository,
    IStaffRepository staffRepository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Label(string id, CancellationToken cancellationToken)
    {
        var staffId = User.FindFirstValue("StaffId");
        var roleClaim = User.FindFirstValue("Role");
        if (string.IsNullOrWhiteSpace(staffId) ||
            !Enum.TryParse<StaffRole>(roleClaim, out var claimedRole) || !Enum.IsDefined(claimedRole))
        {
            return Forbid();
        }

        var staff = await staffRepository.GetByIdAsync(staffId, cancellationToken);
        if (staff is not { IsActive: true } || staff.Role != claimedRole)
        {
            return Forbid();
        }

        var shipment = await shipmentRepository.GetByIdAsync(id, cancellationToken);
        if (shipment is null)
        {
            return NotFound();
        }

        if (claimedRole != StaffRole.Administrator &&
            !string.Equals(shipment.BranchCode?.Value, staff.HomeBranchCode.Value, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        return View(shipment);
    }
}
