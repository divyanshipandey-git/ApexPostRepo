using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ApexPost.Web.Models.Transactions;

public sealed class CounterSaleViewModel
{
    [Required(ErrorMessage = "Select a counter.")]
    public string? CounterId { get; set; }

    [Required(ErrorMessage = "Select a payment method.")]
    [RegularExpression("^(Cash|Card|Upi)$", ErrorMessage = "Select a valid payment method.")]
    public string? PaymentMethod { get; set; }

    public List<CounterSaleProductLineViewModel> Products { get; set; } = [];

    public bool IncludeShipping { get; set; }

    public string? ItemType { get; set; }

    public decimal? WeightKg { get; set; }

    public decimal? LengthCm { get; set; }

    public decimal? WidthCm { get; set; }

    public decimal? HeightCm { get; set; }

    public string? DestinationZone { get; set; }

    public string? ShippingServiceLevel { get; set; }

    public bool IncludeMoneyOrder { get; set; }

    public decimal? MoneyOrderAmount { get; set; }

    public string? MoneyOrderSenderName { get; set; }

    public string? MoneyOrderRecipientName { get; set; }

    public string? IdentityDocumentType { get; set; }

    public string? IdentityMaskedDocumentNumber { get; set; }

    public bool IdentityVerified { get; set; }

    public string? Message { get; set; }

    [ValidateNever]
    public IReadOnlyList<CounterSaleOptionViewModel> CounterOptions { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<CounterSaleProductOptionViewModel> ProductOptions { get; set; } = [];

    [ValidateNever]
    public decimal MoneyOrderFee { get; set; }

    [ValidateNever]
    public decimal MoneyOrderVerificationThreshold { get; set; }
}

public sealed class CounterSaleProductLineViewModel
{
    public string? Sku { get; set; }

    public int? Quantity { get; set; }
}

public sealed record CounterSaleOptionViewModel(string Id, string Name);

public sealed record CounterSaleProductOptionViewModel(string Sku, string Name, decimal UnitPrice, string TaxCategory);
