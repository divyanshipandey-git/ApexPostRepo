using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services.Transactions;

public sealed record CounterSaleRequest(
    string CounterId,
    IReadOnlyCollection<CounterSaleProductRequest> Products,
    PaymentMethod PaymentMethod,
    CounterShippingRequest? Shipping = null,
    CounterMoneyOrderRequest? MoneyOrder = null);

public sealed record CounterSaleProductRequest(string Sku, int Quantity);

public sealed record CounterShippingRequest(
    string ItemType,
    decimal WeightKg,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    string DestinationZone,
    ShippingServiceLevel ServiceLevel);

public sealed record CounterMoneyOrderRequest(
    decimal Amount,
    string SenderName,
    string RecipientName,
    CounterIdentityVerificationRequest? IdentityVerification = null);

public sealed record CounterIdentityVerificationRequest(
    string DocumentType,
    string MaskedDocumentNumber,
    bool IsVerified);
