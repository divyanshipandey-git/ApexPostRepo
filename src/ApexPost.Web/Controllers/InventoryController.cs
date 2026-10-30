using ApexPost.Application.Services.Inventory;
using ApexPost.Web.Models.Inventory;
using ApexPost.Web.Filters;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.InventoryStaff)]
public sealed class InventoryController : Controller
{
    private readonly InventoryInquiryService _inventoryInquiryService;

    public InventoryController(InventoryInquiryService inventoryInquiryService)
    {
        ArgumentNullException.ThrowIfNull(inventoryInquiryService);
        _inventoryInquiryService = inventoryInquiryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? branchCode,
        string? sku,
        CancellationToken cancellationToken)
    {
        var model = new InventoryInquiryViewModel
        {
            BranchCode = branchCode,
            Sku = sku
        };

        if (branchCode is null && sku is null)
        {
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(branchCode))
        {
            ModelState.AddModelError(nameof(model.BranchCode), "Enter a branch code.");
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            ModelState.AddModelError(nameof(model.Sku), "Enter a SKU.");
        }

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(model);
        }

        var result = await _inventoryInquiryService.GetAsync(
            branchCode!,
            sku!,
            cancellationToken);

        return DisplayInquiryResult(model, result);
    }

    [HttpPost]
    [RedisplayInvalidModelState]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Search(
        InventoryInquiryViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Index", model);
        }

        var result = await _inventoryInquiryService.GetAsync(
            model.BranchCode!,
            model.Sku!,
            cancellationToken);

        if (result.Status == InventoryInquiryStatus.Found)
        {
            return RedirectToAction(
                nameof(Index),
                new
                {
                    branchCode = result.Branch!.Code.Value,
                    sku = result.Product!.Sku.Value
                });
        }

        return DisplayInquiryResult(model, result);
    }

    private IActionResult DisplayInquiryResult(
        InventoryInquiryViewModel model,
        InventoryInquiryResult result)
    {
        switch (result.Status)
        {
            case InventoryInquiryStatus.Found:
                model.Result = new InventoryInquiryDisplayModel(
                    result.Branch!.Code.Value,
                    result.Branch.Name,
                    result.Product!.Sku.Value,
                    result.Product.Name,
                    result.Stock!.Quantity);
                return View("Index", model);

            case InventoryInquiryStatus.InvalidBranchCode:
                ModelState.AddModelError(
                    nameof(model.BranchCode),
                    "Branch code must contain only letters and digits and be no longer than 10 characters.");
                Response.StatusCode = StatusCodes.Status400BadRequest;
                break;

            case InventoryInquiryStatus.InvalidSku:
                ModelState.AddModelError(
                    nameof(model.Sku),
                    "SKU must contain only letters and digits and be no longer than 20 characters.");
                Response.StatusCode = StatusCodes.Status400BadRequest;
                break;

            case InventoryInquiryStatus.BranchNotFound:
                model.Message = $"Branch '{model.BranchCode}' was not found.";
                Response.StatusCode = StatusCodes.Status404NotFound;
                break;

            case InventoryInquiryStatus.ProductNotFound:
                model.Message = $"Product with SKU '{model.Sku}' was not found.";
                Response.StatusCode = StatusCodes.Status404NotFound;
                break;

            case InventoryInquiryStatus.StockNotFound:
                model.Message = $"No stock record was found for branch '{model.BranchCode}' and SKU '{model.Sku}'.";
                Response.StatusCode = StatusCodes.Status404NotFound;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }

        return View("Index", model);
    }
}
