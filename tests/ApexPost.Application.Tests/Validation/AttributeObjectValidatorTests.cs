using ApexPost.Application.Services.Transactions;
using ApexPost.Application.Validation;
using ApexPost.Domain.Entities;
using Xunit;

namespace ApexPost.Application.Tests.Validation;

public sealed class AttributeObjectValidatorTests
{
    [Fact]
    public void TryValidateObjectGraph_ValidTransactionRequest_ReturnsNoFailures()
    {
        var request = new CounterSaleRequest(
            "CTR-1",
            [new CounterSaleProductRequest("ENV001", 2)],
            PaymentMethod.Cash,
            MoneyOrder: new CounterMoneyOrderRequest(
                20_000m,
                "Sender",
                "Recipient",
                new CounterIdentityVerificationRequest("Passport", "XXXX1234", true)));

        Assert.True(AttributeObjectValidator.TryValidateObjectGraph(request, out var failures));
        Assert.Empty(failures);
    }

    [Fact]
    public void TryValidateObjectGraph_InvalidMaskedIdentifier_ReturnsMemberFailure()
    {
        var request = new CounterSaleRequest(
            "CTR-1",
            [],
            PaymentMethod.Cash,
            MoneyOrder: new CounterMoneyOrderRequest(
                20_000m,
                "Sender",
                "Recipient",
                new CounterIdentityVerificationRequest("Passport", "12345678", true)));

        Assert.False(AttributeObjectValidator.TryValidateObjectGraph(request, out var failures));
        Assert.Contains(failures, failure => failure.MemberNames.Contains(nameof(CounterIdentityVerificationRequest.MaskedDocumentNumber)));
    }
}
