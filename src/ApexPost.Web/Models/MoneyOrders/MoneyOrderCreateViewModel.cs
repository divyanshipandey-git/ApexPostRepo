using System.ComponentModel.DataAnnotations;
using ApexPost.Web.Models.Transactions;

namespace ApexPost.Web.Models.MoneyOrders;

public sealed class MoneyOrderCreateViewModel
{
    [Required]
    public string? CounterId { get; set; }

    [Required, RegularExpression("^(Cash|Card|Upi)$")]
    public string PaymentMethod { get; set; } = "Cash";

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal Amount { get; set; }

    [Required, StringLength(120)]
    public string SenderName { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string RecipientName { get; set; } = string.Empty;

    [StringLength(80)]
    public string? DocumentType { get; set; }

    [StringLength(32)]
    public string? MaskedDocumentNumber { get; set; }

    public bool IdentityVerified { get; set; }

    public decimal Fee { get; set; }
    public decimal VerificationThreshold { get; set; }
    public IReadOnlyList<CounterSaleOptionViewModel> CounterOptions { get; set; } = [];
    public string? Message { get; set; }
}
