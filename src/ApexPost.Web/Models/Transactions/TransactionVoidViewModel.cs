using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models.Transactions;

public sealed class TransactionVoidViewModel
{
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string TransactionId { get; set; } = string.Empty;

    public string BranchCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string? ExistingReason { get; set; }

    [Required(ErrorMessage = "Enter a reason for voiding this transaction.")]
    [DataType(DataType.MultilineText)]
    public string? VoidReason { get; set; }

    public string? Message { get; set; }
}
