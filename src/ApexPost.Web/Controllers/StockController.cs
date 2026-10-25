using System.Security.Claims;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Inventory;
using ApexPost.Domain.Entities;
using ApexPost.Web.Models.Stock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Roles = "BranchManager,Administrator")]
public sealed class StockController(
    InventoryService inventoryService,
    IBranchRepository branchRepository,
    IProductRepository productRepository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? branchCode, CancellationToken cancellationToken)
    {
        var code = ResolveBranch(branchCode);
        if (code is null)
        {
            return Forbid();
        }

        var stockTask = inventoryService.GetBranchStockAsync(code, cancellationToken);
        var productsTask = productRepository.GetAllAsync(cancellationToken);
        await Task.WhenAll(stockTask, productsTask);
        var products = await productsTask;
        return View(new StockManagementViewModel
        {
            BranchCode = code,
            BranchCodes = User.IsInRole(nameof(StaffRole.Administrator))
                ? (await branchRepository.GetAllAsync(cancellationToken)).Select(branch => branch.Code.Value).ToArray()
                : [code],
            Products = products.Select(product => new ProductOption(product.Sku.Value, product.Name)).ToArray(),
            Rows = (await stockTask).Select(stock => new StockManagementRow(
                stock.Sku.Value,
                products.FirstOrDefault(product => product.Sku == stock.Sku)?.Name ?? stock.Sku.Value,
                stock.Quantity)).OrderBy(row => row.ProductName, StringComparer.OrdinalIgnoreCase).ToArray()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(StockManagementViewModel model, CancellationToken cancellationToken)
    {
        var code = ResolveBranch(model.BranchCode);
        if (code is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid || model.Adjustment == 0)
        {
            ModelState.AddModelError(nameof(model.Adjustment), "Enter a non-zero stock adjustment.");
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return await BuildViewAsync(code, model, cancellationToken);
        }

        var product = await productRepository.GetBySkuAsync(model.Sku, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        try
        {
            var stock = await inventoryService.AdjustQuantityAsync(code, model.Sku, model.Adjustment, cancellationToken);
            if (stock is null)
            {
                ModelState.AddModelError(string.Empty, "Add stock before reducing it; no stock record exists for this SKU.");
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return await BuildViewAsync(code, model, cancellationToken);
            }
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return await BuildViewAsync(code, model, cancellationToken);
        }

        return RedirectToAction(nameof(Index), new { branchCode = code });
    }

    private string? ResolveBranch(string? requestedBranch)
    {
        var claimedBranch = User.FindFirstValue("BranchCode");
        if (User.IsInRole(nameof(StaffRole.Administrator)))
        {
            return string.IsNullOrWhiteSpace(requestedBranch) ? claimedBranch : requestedBranch.Trim().ToUpperInvariant();
        }

        return string.Equals(claimedBranch, requestedBranch ?? claimedBranch, StringComparison.OrdinalIgnoreCase)
            ? claimedBranch
            : null;
    }

    private async Task<IActionResult> BuildViewAsync(
        string branchCode,
        StockManagementViewModel model,
        CancellationToken cancellationToken)
    {
        model.BranchCode = branchCode;
        model.BranchCodes = User.IsInRole(nameof(StaffRole.Administrator))
            ? (await branchRepository.GetAllAsync(cancellationToken)).Select(branch => branch.Code.Value).ToArray()
            : [branchCode];
        var products = await productRepository.GetAllAsync(cancellationToken);
        model.Products = products.Select(product => new ProductOption(product.Sku.Value, product.Name)).ToArray();
        model.Rows = (await inventoryService.GetBranchStockAsync(branchCode, cancellationToken))
            .Select(stock => new StockManagementRow(
                stock.Sku.Value,
                products.FirstOrDefault(product => product.Sku == stock.Sku)?.Name ?? stock.Sku.Value,
                stock.Quantity))
            .OrderBy(row => row.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return View("Index", model);
    }
}
