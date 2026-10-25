using ApexPost.Domain.Entities;
using ApexPost.Application.Validation;
using System.ComponentModel.DataAnnotations;

namespace ApexPost.Application.Services.Transactions;

public sealed record CounterSaleRequest(
    [property: RequiredText(128)] string CounterId,
    IReadOnlyCollection<CounterSaleProductRequest> Products,
    PaymentMethod PaymentMethod,
    CounterShippingRequest? Shipping = null,
    CounterMoneyOrderRequest? MoneyOrder = null);

public sealed record CounterSaleProductRequest(
    [property: RequiredText(20)] string Sku,
    [property: Range(1, 1000)] int Quantity);

public sealed record CounterShippingRequest(
    [property: RequiredText(16)] string ItemType,
    [property: Range(typeof(decimal), "0.001", "1000")] decimal WeightKg,
    [property: Range(typeof(decimal), "0.1", "10000")] decimal LengthCm,
    [property: Range(typeof(decimal), "0.1", "10000")] decimal WidthCm,
    [property: Range(typeof(decimal), "0.1", "10000")] decimal HeightCm,
    [property: RequiredText(32)] string DestinationZone,
    ShippingServiceLevel ServiceLevel);

public sealed record CounterMoneyOrderRequest(
    [property: Range(typeof(decimal), "0.01", "1000000000")] decimal Amount,
    [property: RequiredText(120)] string SenderName,
    [property: RequiredText(120)] string RecipientName,
    CounterIdentityVerificationRequest? IdentityVerification = null);

public sealed record CounterIdentityVerificationRequest(
    [property: RequiredText(80)] string DocumentType,
    [property: MaskedIdentifier] string MaskedDocumentNumber,
    bool IsVerified);
