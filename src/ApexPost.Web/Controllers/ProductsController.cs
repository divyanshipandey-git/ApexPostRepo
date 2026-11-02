using ApexPost.Application.Services.Products;
using ApexPost.Domain.Entities;
using ApexPost.Web.Models.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class ProductsController(
    ProductCatalogService productCatalogService,
    ProductPriceRevisionService priceRevisionService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var products = await productCatalogService.GetAllAsync(cancellationToken);
        return View(products.OrderBy(product => product.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    [Authorize(Roles = nameof(StaffRole.Administrator))]
    [HttpGet]
    public IActionResult PriceRevision()
    {
        SetPriceCategories();
        return View(new PriceRevisionViewModel());
    }

    [Authorize(Roles = nameof(StaffRole.Administrator))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevisePrices(
        PriceRevisionViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid ||
            !Enum.TryParse<ProductCategory>(model.Category, ignoreCase: true, out var category) ||
            !Enum.IsDefined(category))
        {
            ModelState.AddModelError(nameof(model.Category), "Select a valid product category.");
            Response.StatusCode = StatusCodes.Status400BadRequest;
            SetPriceCategories();
            return View("PriceRevision", model);
        }

        var updatedCount = await priceRevisionService.SetCategoryPriceAsync(
            category.ToString(), model.UnitPrice, cancellationToken);
        if (updatedCount == 0)
        {
            ModelState.AddModelError(nameof(model.Category), "No products exist in the selected category.");
            Response.StatusCode = StatusCodes.Status400BadRequest;
            SetPriceCategories();
            return View("PriceRevision", model);
        }

        TempData["PriceRevisionNotice"] =
            $"Updated the unit price for {updatedCount} {category} products.";
        return RedirectToAction(nameof(Index));
    }

    private void SetPriceCategories() =>
        ViewData["PriceCategories"] = Enum.GetNames<ProductCategory>();
}
