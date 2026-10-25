using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models.Stock;

public sealed class StockManagementViewModel
{
    [Required, StringLength(10)]
    public string BranchCode { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Sku { get; set; } = string.Empty;

    [Range(-100000, 100000)]
    public int Adjustment { get; set; }

    public IReadOnlyList<string> BranchCodes { get; set; } = [];
    public IReadOnlyList<ProductOption> Products { get; set; } = [];
    public IReadOnlyList<StockManagementRow> Rows { get; set; } = [];
}

public sealed record ProductOption(string Sku, string Name);
public sealed record StockManagementRow(string Sku, string ProductName, int Quantity);
