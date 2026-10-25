
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public sealed class CounterTransactionTests
{
    private static CounterTransaction CreateTransaction(
        IEnumerable<TaxRate>? taxRates = null)
    {
        return new CounterTransaction(
            "TXN001",
            new BranchCode("BR001"),
            "COUNTER01",
            "STAFF001",
            taxRates);
    }

    private static TransactionLineItem CreateLineItem()
    {
        return new TransactionLineItem(
            "Envelope",
            "Retail",
            2,
            new Money(50m));
    }

    private static TaxRate[] CreateTaxRates() =>
    [
        new TaxRate(TaxCategory.Standard, 0.18m),
        new TaxRate(TaxCategory.Reduced, 0.05m),
        new TaxRate(TaxCategory.ZeroRated, 0m),
        new TaxRate(TaxCategory.Exempt, 0m)
    ];

    [Fact]
    public void Constructor_ValidValues_CreatesOpenTransaction()
    {
        var transaction = CreateTransaction();

        Assert.Equal("TXN001", transaction.Id);
        Assert.Equal("BR001", transaction.BranchCode.Value);
        Assert.Equal("COUNTER01", transaction.CounterId);
        Assert.Equal("STAFF001", transaction.ClerkId);
        Assert.Equal(TransactionStatus.Open, transaction.Status);
    }

    [Fact]
    public void AddLineItem_OpenTransaction_AddsItem()
    {
        var transaction = CreateTransaction();

        transaction.AddLineItem(CreateLineItem());

        Assert.Single(transaction.LineItems);
        Assert.Equal(100m, transaction.Subtotal.Amount);
        Assert.Equal(100m, transaction.Total.Amount);
    }

    [Fact]
    public void CompletePayment_WithItems_MarksTransactionPaid()
    {
        var transaction = CreateTransaction();
        transaction.AddLineItem(CreateLineItem());

        transaction.CompletePayment(PaymentMethod.Cash);

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(PaymentMethod.Cash, transaction.PaymentMethod);
    }

    [Fact]
    public void CompletePayment_WithoutItems_ThrowsException()
    {
        var transaction = CreateTransaction();

        Assert.Throws<InvalidOperationException>(
            () => transaction.CompletePayment(PaymentMethod.Cash));
    }

    [Fact]
    public void AddLineItem_AfterPayment_ThrowsException()
    {
        var transaction = CreateTransaction();
        transaction.AddLineItem(CreateLineItem());
        transaction.CompletePayment(PaymentMethod.Cash);

        Assert.Throws<InvalidOperationException>(
            () => transaction.AddLineItem(CreateLineItem()));
    }

    [Fact]
    public void Void_PaidTransactionWithReason_MarksTransactionVoided()
    {
        var transaction = CreateTransaction();
        transaction.AddLineItem(CreateLineItem());
        transaction.CompletePayment(PaymentMethod.Cash);

        transaction.Void("Duplicate transaction");

        Assert.Equal(TransactionStatus.Voided, transaction.Status);
        Assert.Equal("Duplicate transaction", transaction.Reason);
    }

    [Fact]
    public void Refund_PaidTransactionWithReason_MarksTransactionRefunded()
    {
        var transaction = CreateTransaction();
        transaction.AddLineItem(CreateLineItem());
        transaction.CompletePayment(PaymentMethod.Upi);

        transaction.Refund("Customer refund");

        Assert.Equal(TransactionStatus.Refunded, transaction.Status);
        Assert.Equal("Customer refund", transaction.Reason);
    }

    [Fact]
    public void Void_WithoutReason_ThrowsException()
    {
        var transaction = CreateTransaction();
        transaction.AddLineItem(CreateLineItem());
        transaction.CompletePayment(PaymentMethod.Cash);

        Assert.Throws<ArgumentException>(
            () => transaction.Void(" "));
    }

    [Fact]
    public void Void_OpenTransaction_ThrowsAndLeavesStateOpen()
    {
        var transaction = CreateTransaction();

        Assert.Throws<InvalidOperationException>(() => transaction.Void("Duplicate transaction"));
        Assert.Equal(TransactionStatus.Open, transaction.Status);
        Assert.Null(transaction.Reason);
    }

    [Fact]
    public void Void_AlreadyVoidedTransaction_ThrowsAndPreservesOriginalReason()
    {
        var transaction = CreateTransaction();
        transaction.AddLineItem(CreateLineItem());
        transaction.CompletePayment(PaymentMethod.Cash);
        transaction.Void("Original reason");

        Assert.Throws<InvalidOperationException>(() => transaction.Void("Second reason"));
        Assert.Equal(TransactionStatus.Voided, transaction.Status);
        Assert.Equal("Original reason", transaction.Reason);
    }

    [Fact]
    public void Refund_OpenTransaction_ThrowsException()
    {
        var transaction = CreateTransaction();

        Assert.Throws<InvalidOperationException>(
            () => transaction.Refund("Customer refund"));
    }

    [Fact]
    public void Refund_WithoutReason_ThrowsException()
    {
        var transaction = CreateTransaction();
        transaction.AddLineItem(CreateLineItem());
        transaction.CompletePayment(PaymentMethod.Cash);

        Assert.Throws<ArgumentException>(
            () => transaction.Refund(""));
    }

    [Fact]
    public void CompletePayment_InvalidPaymentMethod_ThrowsException()
    {
        var transaction = CreateTransaction();
        transaction.AddLineItem(CreateLineItem());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => transaction.CompletePayment((PaymentMethod)999));

        Assert.Equal(TransactionStatus.Open, transaction.Status);
    }

    [Fact]
    public void CalculateTax_StandardRate_ReturnsCorrectTax()
    {
        var transaction = CreateTransaction(CreateTaxRates());

        transaction.AddLineItem(
            new TransactionLineItem(
                "Packaging",
                "Retail",
                2,
                new Money(100m),
                TaxCategory.Standard));

        Assert.Equal(36m, transaction.CalculateTax().Amount);
    }

    [Fact]
    public void CalculateTotal_IncludesSubtotalAndTax()
    {
        var transaction = CreateTransaction(CreateTaxRates());

        transaction.AddLineItem(
            new TransactionLineItem(
                "Packaging",
                "Retail",
                2,
                new Money(100m),
                TaxCategory.Standard));

        Assert.Equal(200m, transaction.Subtotal.Amount);
        Assert.Equal(36m, transaction.Tax.Amount);
        Assert.Equal(236m, transaction.CalculateTotal().Amount);
        Assert.Equal(236m, transaction.Total.Amount);
    }

    [Fact]
    public void CalculateTax_MixedCategories_CalculatesTaxForEachLine()
    {
        var transaction = CreateTransaction(CreateTaxRates());

        transaction.AddLineItem(
            new TransactionLineItem(
                "Notebook",
                "Retail",
                1,
                new Money(100m),
                TaxCategory.Standard));

        transaction.AddLineItem(
            new TransactionLineItem(
                "Exempt service",
                "Service",
                1,
                new Money(50m),
                TaxCategory.Exempt));

        Assert.Equal(18m, transaction.CalculateTax().Amount);
    }

    [Fact]
    public void CalculateTax_MissingCategoryRate_ThrowsException()
    {
        var taxRates = new[]
        {
            new TaxRate(TaxCategory.Exempt, 0m)
        };

        var transaction = CreateTransaction(taxRates);

        transaction.AddLineItem(
            new TransactionLineItem(
                "Notebook",
                "Retail",
                1,
                new Money(100m),
                TaxCategory.Standard));

        Assert.Throws<InvalidOperationException>(
            () => transaction.CalculateTax());
    }
}
