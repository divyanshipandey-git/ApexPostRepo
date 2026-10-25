using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models.Inventory;

public sealed class InventoryInquiryViewModel
{
    private string? _branchCode;
    private string? _sku;

    [Display(Name = "Branch code")]
    [Required(ErrorMessage = "Enter a branch code.")]
    [StringLength(10, MinimumLength = 1, ErrorMessage = "Branch code must be no longer than 10 characters.")]
    [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Branch code must contain only letters and digits.")]
    public string? BranchCode
    {
        get => _branchCode;
        set => _branchCode = value?.Trim();
    }

    [Display(Name = "SKU")]
    [Required(ErrorMessage = "Enter a SKU.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "SKU must be no longer than 20 characters.")]
    [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "SKU must contain only letters and digits.")]
    public string? Sku
    {
        get => _sku;
        set => _sku = value?.Trim();
    }

    public InventoryInquiryDisplayModel? Result { get; set; }
    public string? Message { get; set; }
}

public sealed record InventoryInquiryDisplayModel(
    string BranchCode,
    string BranchName,
    string Sku,
    string ProductName,
    int Quantity);
