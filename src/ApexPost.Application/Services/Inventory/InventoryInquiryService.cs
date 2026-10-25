using ApexPost.Application.Services.Branches;
using ApexPost.Application.Services.Products;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services.Inventory;

public sealed class InventoryInquiryService
{
    private readonly BranchService _branchService;
    private readonly ProductCatalogService _productCatalogService;
    private readonly InventoryService _inventoryService;

    public InventoryInquiryService(
        BranchService branchService,
        ProductCatalogService productCatalogService,
        InventoryService inventoryService)
    {
        ArgumentNullException.ThrowIfNull(branchService);
        ArgumentNullException.ThrowIfNull(productCatalogService);
        ArgumentNullException.ThrowIfNull(inventoryService);

        _branchService = branchService;
        _productCatalogService = productCatalogService;
        _inventoryService = inventoryService;
    }

    public async Task<InventoryInquiryResult> GetAsync(
        string branchCode,
        string sku,
        CancellationToken cancellationToken = default)
    {
        BranchCode validatedBranchCode;
        try
        {
            validatedBranchCode = new BranchCode(branchCode);
        }
        catch (ArgumentException)
        {
            return new InventoryInquiryResult(InventoryInquiryStatus.InvalidBranchCode);
        }

        Sku validatedSku;
        try
        {
            validatedSku = new Sku(sku);
        }
        catch (ArgumentException)
        {
            return new InventoryInquiryResult(InventoryInquiryStatus.InvalidSku);
        }

        var branch = await _branchService.GetByCodeAsync(
            validatedBranchCode.Value,
            cancellationToken);

        if (branch is null)
        {
            return new InventoryInquiryResult(InventoryInquiryStatus.BranchNotFound);
        }

        var product = await _productCatalogService.GetBySkuAsync(
            validatedSku.Value,
            cancellationToken);

        if (product is null)
        {
            return new InventoryInquiryResult(InventoryInquiryStatus.ProductNotFound);
        }

        var stock = await _inventoryService.GetStockAsync(
            validatedBranchCode.Value,
            validatedSku.Value,
            cancellationToken);

        if (stock is null)
        {
            return new InventoryInquiryResult(InventoryInquiryStatus.StockNotFound);
        }

        return new InventoryInquiryResult(
            InventoryInquiryStatus.Found,
            branch,
            product,
            stock);
    }
}
