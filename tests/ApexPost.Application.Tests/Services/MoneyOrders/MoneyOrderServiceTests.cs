
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.MoneyOrders;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Application.Tests.Services.MoneyOrders;

public sealed class MoneyOrderServiceTests
{
    [Fact]
    public void Constructor_WhenRepositoryIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MoneyOrderService(
                null!,
                new MoneyOrderPolicy(new Money(1000m))));
    }

    [Fact]
    public void Constructor_WhenPolicyIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MoneyOrderService(
                new FakeMoneyOrderRepository(),
                null!));
    }

    [Fact]
    public async Task CreateAsync_WhenAmountIsBelowThreshold_SavesOrder()
    {
        var repository = new FakeMoneyOrderRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(
            "MO001",
            new Money(500m),
            "Sender",
            "Recipient",
            new Money(10m),
            null,
            new BranchCode("IND01"),
            "TXN001");

        Assert.NotNull(result);
        Assert.Same(result, repository.AddedMoneyOrder);
        Assert.Equal("IND01", result.BranchCode);
        Assert.Equal("TXN001", result.TransactionId);
    }

    [Fact]
    public async Task CreateAsync_WhenAmountEqualsThreshold_SavesOrderWithoutVerification()
    {
        var repository = new FakeMoneyOrderRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(
            "MO002",
            new Money(1000m),
            "Sender",
            "Recipient",
            new Money(10m),
            null,
            new BranchCode("IND01"),
            "TXN002");

        Assert.NotNull(result);
        Assert.Same(result, repository.AddedMoneyOrder);
        Assert.Equal("IND01", result.BranchCode);
        Assert.Equal("TXN002", result.TransactionId);
    }

    [Fact]
    public async Task CreateAsync_WhenHighValueOrderHasNoVerification_ThrowsAndDoesNotSave()
    {
        var repository = new FakeMoneyOrderRepository();
        var service = CreateService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                "MO003",
                new Money(1500m),
                "Sender",
                "Recipient",
                new Money(10m),
                null,
                new BranchCode("IND01"),
                "TXN003"));

        Assert.Null(repository.AddedMoneyOrder);
    }

    [Fact]
    public async Task CreateAsync_WhenHighValueOrderHasFailedVerification_ThrowsAndDoesNotSave()
    {
        var repository = new FakeMoneyOrderRepository();
        var service = CreateService(repository);
        var verification = CreateVerification(isVerified: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                "MO004",
                new Money(1500m),
                "Sender",
                "Recipient",
                new Money(10m),
                verification,
                new BranchCode("IND01"),
                "TXN004"));

        Assert.Null(repository.AddedMoneyOrder);
    }

    [Fact]
    public async Task CreateAsync_WhenHighValueOrderHasSuccessfulVerification_SavesOrder()
    {
        var repository = new FakeMoneyOrderRepository();
        var service = CreateService(repository);
        var verification = CreateVerification(isVerified: true);

        var result = await service.CreateAsync(
            "MO005",
            new Money(1500m),
            "Sender",
            "Recipient",
            new Money(10m),
            verification,
            new BranchCode("IND01"),
            "TXN005");

        Assert.NotNull(result);
        Assert.Same(result, repository.AddedMoneyOrder);
        Assert.Equal("IND01", result.BranchCode);
        Assert.Equal("TXN005", result.TransactionId);
        Assert.Equal("XXXX1234", result.IdentityVerification!.MaskedDocumentNumber);
    }

    [Theory]
    [InlineData("", "Recipient")]
    [InlineData("Sender", "")]
    public async Task CreateAsync_WhenNamesAreBlank_Throws(
        string sender,
        string recipient)
    {
        var service = CreateService(new FakeMoneyOrderRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(
                "MO006",
                new Money(100m),
                sender,
                recipient,
                new Money(10m),
                null,
                new BranchCode("IND01"),
                "TXN006"));
    }

    [Fact]
    public async Task CreateAsync_WhenCancelled_Throws()
    {
        var service = CreateService(new FakeMoneyOrderRepository());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CreateAsync(
                "MO007",
                new Money(100m),
                "Sender",
                "Recipient",
                new Money(10m),
                null,
                new BranchCode("IND01"),
                "TXN007",
                cancellation.Token));
    }

    private static MoneyOrderService CreateService(
        FakeMoneyOrderRepository repository) =>
        new(repository, new MoneyOrderPolicy(new Money(1000m)));

    private static IdentityVerification CreateVerification(bool isVerified) =>
        new(
            "IDV001",
            "Government ID",
            "XXXX1234",
            isVerified,
            DateTimeOffset.UtcNow);

    private sealed class FakeMoneyOrderRepository : IMoneyOrderRepository
    {
        public MoneyOrder? AddedMoneyOrder { get; private set; }

        public Task<MoneyOrder?> GetByIdAsync(
            string moneyOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MoneyOrder?>(null);

        public Task<IReadOnlyList<MoneyOrder>> GetByBranchAsync(
            string branchCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MoneyOrder>>(
                Array.Empty<MoneyOrder>());

        public Task AddAsync(
            MoneyOrder moneyOrder,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddedMoneyOrder = moneyOrder;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            MoneyOrder moneyOrder,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(
            string moneyOrderId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
