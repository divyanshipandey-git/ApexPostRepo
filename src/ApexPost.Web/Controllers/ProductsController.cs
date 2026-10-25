using ApexPost.Application.Services.Products;
using ApexPost.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class ProductsController(ProductCatalogService productCatalogService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var products = await productCatalogService.GetAllAsync(cancellationToken);
        return View(products.OrderBy(product => product.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
