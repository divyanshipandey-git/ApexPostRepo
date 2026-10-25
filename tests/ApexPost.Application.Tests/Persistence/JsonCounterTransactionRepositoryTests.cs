
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

public sealed class JsonCounterTransactionRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_WhenMissing_ReturnsNull()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonCounterTransactionRepository(directory.CreateOptions());

        var result = await repository.GetByIdAsync("TXN001");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByBranchAsync_WhenEmpty_ReturnsEmptyCollection()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonCounterTransactionRepository(directory.CreateOptions());

        var result = await repository.GetByBranchAsync("BR001");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonCounterTransactionRepository(directory.CreateOptions());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetByIdAsync("TXN001", cancellation.Token));
    }

    [Fact]
    public async Task PaidTransactionCanBeReloadedVoidedAndPersisted()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var repository = new JsonCounterTransactionRepository(directory.CreateOptions());
        var transaction = new CounterTransaction(
            "TXN-VOID-1",
            new BranchCode("IND01"),
            "COUNTER01",
            "CLERK01",
            [
                new TaxRate(TaxCategory.Standard, 0.18m),
                new TaxRate(TaxCategory.Reduced, 0.05m),
                new TaxRate(TaxCategory.ZeroRated, 0m),
                new TaxRate(TaxCategory.Exempt, 0m)
            ]);
        transaction.AddLineItem(new TransactionLineItem(
            "Postage",
            "Service",
            1,
            new Money(100m),
            TaxCategory.Standard));
        transaction.CompletePayment(PaymentMethod.Cash);

        await repository.AddAsync(transaction);
        var restartedRepository = new JsonCounterTransactionRepository(directory.CreateOptions());
        var reloaded = await restartedRepository.GetByIdAsync("TXN-VOID-1");

        Assert.NotNull(reloaded);
        Assert.Equal(TransactionStatus.Paid, reloaded.Status);
        Assert.Equal(PaymentMethod.Cash, reloaded.PaymentMethod);
        Assert.Single(reloaded.LineItems);
        Assert.Equal(118m, reloaded.Total.Amount);

        reloaded.Void("Duplicate charge");
        await restartedRepository.UpdateAsync(reloaded);
        var saved = await restartedRepository.GetByIdAsync("TXN-VOID-1");

        Assert.NotNull(saved);
        Assert.Equal(TransactionStatus.Voided, saved.Status);
        Assert.Equal("Duplicate charge", saved.Reason);
        Assert.Single(saved.LineItems);
    }
}
