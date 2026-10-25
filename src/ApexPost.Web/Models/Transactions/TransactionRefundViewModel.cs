using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models.Transactions;

public sealed class TransactionRefundViewModel
{
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string TransactionId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a reason for refunding this transaction.")]
    [StringLength(500, ErrorMessage = "The refund reason must be no longer than 500 characters.")]
    public string? RefundReason { get; set; }
}
