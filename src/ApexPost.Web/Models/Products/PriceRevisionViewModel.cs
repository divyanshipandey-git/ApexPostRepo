using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models.Products;

public sealed class PriceRevisionViewModel
{
    [Required]
    [StringLength(32)]
    public string Category { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "1000000")]
    public decimal UnitPrice { get; set; }
}
