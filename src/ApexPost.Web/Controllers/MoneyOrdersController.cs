using System.Security.Claims;
using ApexPost.Application.Services.Staff;
using ApexPost.Application.Services.Transactions;
using ApexPost.Domain.Entities;
using ApexPost.Web.Models.MoneyOrders;
using ApexPost.Web.Models.Transactions;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.CounterSales)]
public sealed class MoneyOrdersController(CounterSaleService counterSaleService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var staff = CurrentStaff();
        if (staff is null)
        {
            return Forbid();
        }

        var options = await counterSaleService.GetOptionsAsync(staff, cancellationToken);
        return options is null ? Forbid() : View(ToViewModel(options));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        MoneyOrderCreateViewModel model,
        CancellationToken cancellationToken)
    {
        var staff = CurrentStaff();
        if (staff is null)
        {
            return Forbid();
        }

        if (!Enum.TryParse<PaymentMethod>(model.PaymentMethod, out var paymentMethod) ||
            !Enum.IsDefined(paymentMethod))
        {
            ModelState.AddModelError(nameof(model.PaymentMethod), "Select a supported payment method.");
        }

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Create", await PopulateAsync(model, staff, cancellationToken));
        }

        var hasIdentityDetails = model.IdentityVerified ||
            !string.IsNullOrWhiteSpace(model.DocumentType) ||
            !string.IsNullOrWhiteSpace(model.MaskedDocumentNumber);
        var verification = hasIdentityDetails
            ? new CounterIdentityVerificationRequest(
                model.DocumentType ?? string.Empty,
                model.MaskedDocumentNumber ?? string.Empty,
                model.IdentityVerified)
            : null;
        var result = await counterSaleService.CompleteAsync(
            new CounterSaleRequest(
                model.CounterId!,
                [],
                paymentMethod,
                MoneyOrder: new CounterMoneyOrderRequest(
                    model.Amount,
                    model.SenderName,
                    model.RecipientName,
                    verification)),
            staff,
            cancellationToken);

        if (result.Status == CounterSaleStatus.Completed)
        {
            return RedirectToAction("Receipt", "Transactions", new { id = result.Transaction!.Id });
        }

        model.Message = result.Status switch
        {
            CounterSaleStatus.MoneyOrderVerificationRequired =>
                "This amount requires a successful identity check in this money-order transaction.",
            CounterSaleStatus.BranchClosed => "The branch is closed and cannot accept transactions.",
            CounterSaleStatus.CounterInactive or CounterSaleStatus.CounterNotFound =>
                "Select an active counter at your branch.",
            _ => "The money order could not be completed. Review the entered details and try again."
        };
        Response.StatusCode = result.Status is CounterSaleStatus.BranchClosed or CounterSaleStatus.CounterInactive
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
        return View("Create", await PopulateAsync(model, staff, cancellationToken));
    }

    private async Task<MoneyOrderCreateViewModel> PopulateAsync(
        MoneyOrderCreateViewModel model,
        AuthenticatedStaff staff,
        CancellationToken cancellationToken)
    {
        var options = await counterSaleService.GetOptionsAsync(staff, cancellationToken);
        if (options is not null)
        {
            model.CounterOptions = options.Counters
                .Select(counter => new CounterSaleOptionViewModel(counter.Id, counter.Name))
                .ToArray();
            model.Fee = options.MoneyOrderFee;
            model.VerificationThreshold = options.MoneyOrderVerificationThreshold;
        }

        return model;
    }

    private static MoneyOrderCreateViewModel ToViewModel(CounterSaleOptions options) => new()
    {
        CounterOptions = options.Counters.Select(counter => new CounterSaleOptionViewModel(counter.Id, counter.Name)).ToArray(),
        Fee = options.MoneyOrderFee,
        VerificationThreshold = options.MoneyOrderVerificationThreshold
    };

    private AuthenticatedStaff? CurrentStaff()
    {
        var staffId = User.FindFirstValue("StaffId");
        var branchCode = User.FindFirstValue("BranchCode");
        return Enum.TryParse<StaffRole>(User.FindFirstValue("Role"), out var role) &&
               Enum.IsDefined(role) && !string.IsNullOrWhiteSpace(staffId) && !string.IsNullOrWhiteSpace(branchCode)
            ? new AuthenticatedStaff(staffId, role, branchCode)
            : null;
    }
}
