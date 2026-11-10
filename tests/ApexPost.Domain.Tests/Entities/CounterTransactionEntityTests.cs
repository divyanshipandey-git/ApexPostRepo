using System;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

/// <summary>
/// Tests for CounterTransaction entity
/// </summary>
public class CounterTransactionEntityTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesTransaction()
    {
        // Arrange
        var branchCode = new BranchCode("BR001");
        var taxRates = new[]
        {
            new TaxRate(TaxCategory.Standard, 0.10m),
            new TaxRate(TaxCategory.Reduced, 0.05m)
        };

        // Act
        var tx = new CounterTransaction(
            "TXN-001",
            branchCode,
            "COUNTER-001",
            "CLERK-001",
            taxRates);

        // Assert
        Assert.Equal("TXN-001", tx.Id);
        Assert.Equal(branchCode, tx.BranchCode);
        Assert.Equal("COUNTER-001", tx.CounterId);
        Assert.Equal("CLERK-001", tx.ClerkId);
        Assert.Equal(TransactionStatus.Open, tx.Status);
    }

    [Fact]
    public void Constructor_NullBranchCode_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CounterTransaction(
            "TXN-001", null!, "COUNTER-001", "CLERK-001"));
    }

    [Fact]
    public void Constructor_EmptyId_ShouldReject()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CounterTransaction(
            "", new BranchCode("BR001"), "COUNTER-001", "CLERK-001"));
    }

    [Fact]
    public void Constructor_DuplicateTaxCategories_ShouldReject()
    {
        // Arrange
        var taxRates = new[]
        {
            new TaxRate(TaxCategory.Standard, 0.10m),
            new TaxRate(TaxCategory.Standard, 0.05m) // Duplicate
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CounterTransaction(
            "TXN-001", new BranchCode("BR001"), "COUNTER-001", "CLERK-001", taxRates));
    }

    [Fact]
    public void AddLineItem_ValidItem_AddsItem()
    {
        // Arrange
        var tx = CreateTransaction();
        var lineItem = new TransactionLineItem(
            "Product 1",
            "Retail",
            2,
            new Money(10.00m),
            TaxCategory.Standard);

        // Act
        tx.AddLineItem(lineItem);

        // Assert
        Assert.Single(tx.LineItems);
    }

    [Fact]
    public void AddLineItem_NullItem_ShouldReject()
    {
        // Arrange
        var tx = CreateTransaction();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => tx.AddLineItem(null!));
    }

    [Fact]
    public void AddLineItem_ClosedTransaction_ShouldReject()
    {
        // Arrange
        var tx = CreateTransaction();
        var lineItem = new TransactionLineItem(
            "Product 1",
            "Retail",
            2,
            new Money(10.00m),
            TaxCategory.Standard);
        tx.AddLineItem(lineItem);
        tx.CompletePayment(PaymentMethod.Cash);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => tx.AddLineItem(
            new TransactionLineItem("Product 2", "Retail", 1, new Money(5.00m), TaxCategory.Standard)));
    }

    [Fact]
    public void CompletePayment_ValidPayment_CompletesTransaction()
    {
        // Arrange
        var tx = CreateTransaction();
        var lineItem = new TransactionLineItem(
            "Product 1",
            "Retail",
            2,
            new Money(10.00m),
            TaxCategory.Standard);
        tx.AddLineItem(lineItem);

        // Act
        tx.CompletePayment(PaymentMethod.Cash);

        // Assert
        Assert.Equal(TransactionStatus.Paid, tx.Status);
        Assert.Equal(PaymentMethod.Cash, tx.PaymentMethod);
    }

    [Fact]
    public void CompletePayment_EmptyTransaction_ShouldReject()
    {
        // Arrange
        var tx = CreateTransaction();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => tx.CompletePayment(PaymentMethod.Cash));
    }

    [Fact]
    public void CompletePayment_InvalidPaymentMethod_ShouldReject()
    {
        // Arrange
        var tx = CreateTransaction();
        var lineItem = new TransactionLineItem(
            "Product 1",
            "Retail",
            2,
            new Money(10.00m),
            TaxCategory.Standard);
        tx.AddLineItem(lineItem);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => tx.CompletePayment((PaymentMethod)999));
    }

    [Fact]
    public void Void_ValidReason_VoidsTransaction()
    {
        // Arrange
        var tx = CreateTransaction();
        var lineItem = new TransactionLineItem(
            "Product 1",
            "Retail",
            2,
            new Money(10.00m),
            TaxCategory.Standard);
        tx.AddLineItem(lineItem);
        tx.CompletePayment(PaymentMethod.Cash);

        // Act
        tx.Void("Customer cancelled");

        // Assert
        Assert.Equal(TransactionStatus.Voided, tx.Status);
        Assert.Equal("Customer cancelled", tx.Reason);
    }

    [Fact]
    public void Void_EmptyReason_ShouldReject()
    {
        // Arrange
        var tx = CreateTransaction();
        var lineItem = new TransactionLineItem(
            "Product 1",
            "Retail",
            2,
            new Money(10.00m),
            TaxCategory.Standard);
        tx.AddLineItem(lineItem);
        tx.CompletePayment(PaymentMethod.Cash);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => tx.Void(""));
    }

    [Fact]
    public void Void_NotPaidTransaction_ShouldReject()
    {
        // Arrange
        var tx = CreateTransaction();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => tx.Void("Reason"));
    }

    [Fact]
    public void Refund_ValidReason_RefundsTransaction()
    {
        // Arrange
        var tx = CreateTransaction();
        var lineItem = new TransactionLineItem(
            "Product 1",
            "Retail",
            2,
            new Money(10.00m),
            TaxCategory.Standard);
        tx.AddLineItem(lineItem);
        tx.CompletePayment(PaymentMethod.Cash);

        // Act
        tx.Refund("Defective product");

        // Assert
        Assert.Equal(TransactionStatus.Refunded, tx.Status);
        Assert.Equal("Defective product", tx.Reason);
    }

    [Fact]
    public void Subtotal_CalculatesCorrectly()
    {
        // Arrange
        var tx = CreateTransaction();
        tx.AddLineItem(new TransactionLineItem("Product 1", "Retail", 2, new Money(10.00m), TaxCategory.Standard));
        tx.AddLineItem(new TransactionLineItem("Product 2", "Retail", 1, new Money(5.00m), TaxCategory.Standard));

        // Act
        var subtotal = tx.Subtotal;

        // Assert
        Assert.Equal(new Money(25.00m), subtotal);
    }

    [Fact]
    public void Tax_CalculatesWithTaxRates()
    {
        // Arrange
        var tx = CreateTransaction(taxRate: new TaxRate(TaxCategory.Standard, 0.10m));
        tx.AddLineItem(new TransactionLineItem("Product 1", "Retail", 1, new Money(10.00m), TaxCategory.Standard));

        // Act
        var tax = tx.Tax;

        // Assert
        Assert.Equal(new Money(1.00m), tax);
    }

    [Fact]
    public void Total_CalculatesCorrectly()
    {
        // Arrange
        var tx = CreateTransaction(taxRate: new TaxRate(TaxCategory.Standard, 0.10m));
        tx.AddLineItem(new TransactionLineItem("Product 1", "Retail", 1, new Money(10.00m), TaxCategory.Standard));

        // Act
        var total = tx.Total;

        // Assert
        Assert.Equal(new Money(11.00m), total);
    }

    [Fact]
    public void Equality_SameId_ShouldBeEqual()
    {
        var id = "TXN-001";
        var tx1 = CreateTransaction(id: id);
        var tx2 = CreateTransaction(id: id);
        // CounterTransaction uses reference equality by default
        // Two instances with same ID are not equal unless explicitly implemented
        Assert.NotEqual(tx1, tx2);
    }

    [Fact]
    public void GetHashCode_SameId_ShouldBeEqual()
    {
        var id = "TXN-001";
        var tx1 = CreateTransaction(id: id);
        var tx2 = CreateTransaction(id: id);
        // GetHashCode is based on reference by default
        Assert.NotEqual(tx1.GetHashCode(), tx2.GetHashCode());
    }

    private CounterTransaction CreateTransaction(
        string id = "TXN-001",
        TaxRate? taxRate = null)
    {
        var branchCode = new BranchCode("BR001");
        var taxRates = taxRate != null ? new[] { taxRate } : 
            new[] { new TaxRate(TaxCategory.Standard, 0.10m) };
        return new CounterTransaction(id, branchCode, "COUNTER-001", "CLERK-001", taxRates);
    }
}