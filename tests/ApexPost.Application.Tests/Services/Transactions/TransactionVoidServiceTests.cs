using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Notifications;
using ApexPost.Application.Services.Transactions;
using ApexPost.Application.Notifications;
using ApexPost.Application.Tests.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;
using StaffEntity = ApexPost.Domain.Entities.Staff;

namespace ApexPost.Application.Tests.Services.Transactions;

public sealed class TransactionVoidServiceTests
{
    [Fact]
    public async Task RefundAsync_WhenPaidByManager_PersistsFullRefundAndPublishesNotification()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var inventory = new JsonInventoryRepository(directory.CreateOptions());
        await inventory.AddAsync(new InventoryStock(new BranchCode("IND01"), new Sku("SKU001"), 1));
        var repository = new FakeCounterTransactionRepository(CreatePaidTransaction());
        var access = new TransactionVoidService(
            repository,
            new FakeStaffRepository(CreateStaff(StaffRole.BranchManager, "IND01")),
            inventoryRepository: inventory);
        var notifications = new SimulatedNotificationService();
        var service = new TransactionRefundService(repository, access, notifications);

        var result = await service.RefundAsync("TXN-APP-1", "Customer return", "staff-test-1");

        Assert.Equal(TransactionRefundStatus.Refunded, result.Status);
        Assert.Equal(TransactionStatus.Refunded, repository.Transaction!.Status);
        Assert.Equal("Customer return", repository.Transaction.Reason);
        Assert.Equal(1, repository.UpdateCount);
        Assert.Equal(2, (await inventory.GetByBranchAndSkuAsync("IND01", "SKU001"))!.Quantity);
        Assert.Equal(NotificationType.TransactionRefund,
            Assert.Single(notifications.GetPublishedNotifications()).Type);
    }

    [Fact]
    public async Task RefundAsync_AllowsAdministratorAcrossBranches()
    {
        var repository = new FakeCounterTransactionRepository(CreatePaidTransaction("IND02"));
        var access = new TransactionVoidService(
            repository,
            new FakeStaffRepository(CreateStaff(StaffRole.Administrator, "IND01")));
        var service = new TransactionRefundService(
            repository,
            access,
            new SimulatedNotificationService());

        var result = await service.RefundAsync("TXN-APP-1", "Approved refund", "staff-test-1");

        Assert.Equal(TransactionRefundStatus.Refunded, result.Status);
        Assert.Equal(TransactionStatus.Refunded, repository.Transaction!.Status);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Theory]
    [InlineData(StaffRole.CounterClerk, "IND01", "IND01")]
    [InlineData(StaffRole.BranchManager, "IND02", "IND01")]
    public async Task RefundAsync_WhenCallerIsUnauthorizedOrWrongBranch_DoesNotMutate(
        StaffRole role,
        string staffBranch,
        string transactionBranch)
    {
        var repository = new FakeCounterTransactionRepository(CreatePaidTransaction(transactionBranch));
        var access = new TransactionVoidService(
            repository,
            new FakeStaffRepository(CreateStaff(role, staffBranch)));
        var service = new TransactionRefundService(
            repository,
            access,
            new SimulatedNotificationService());

        var result = await service.RefundAsync("TXN-APP-1", "Customer return", "staff-test-1");

        Assert.Equal(TransactionRefundStatus.Forbidden, result.Status);
        Assert.Equal(TransactionStatus.Paid, repository.Transaction!.Status);
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public async Task RefundAsync_WhenReasonMissingOrTransactionNotPaid_DoesNotPersist()
    {
        var repository = new FakeCounterTransactionRepository(CreatePaidTransaction());
        var access = new TransactionVoidService(
            repository,
            new FakeStaffRepository(CreateStaff(StaffRole.BranchManager, "IND01")));
        var service = new TransactionRefundService(
            repository,
            access,
            new SimulatedNotificationService());

        var missingReason = await service.RefundAsync("TXN-APP-1", " ", "staff-test-1");
        Assert.Equal(TransactionRefundStatus.InvalidReason, missingReason.Status);
        Assert.Equal(TransactionStatus.Paid, repository.Transaction!.Status);

        repository.Transaction.Void("Already voided");
        var invalidState = await service.RefundAsync("TXN-APP-1", "Customer return", "staff-test-1");

        Assert.Equal(TransactionRefundStatus.InvalidState, invalidState.Status);
        Assert.Equal(TransactionStatus.Voided, repository.Transaction.Status);
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public async Task VoidAsync_WhenPaidBySameBranchManager_PersistsVoidedStateAndReason()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var inventory = new JsonInventoryRepository(directory.CreateOptions());
        await inventory.AddAsync(new InventoryStock(new BranchCode("IND01"), new Sku("SKU001"), 1));
        var repository = new FakeCounterTransactionRepository(CreatePaidTransaction());
        var staff = new FakeStaffRepository(CreateStaff(StaffRole.BranchManager, "IND01"));
        var service = new TransactionVoidService(repository, staff, inventoryRepository: inventory);

        var result = await service.VoidAsync(
            "TXN-APP-1",
            "Duplicate sale",
            "staff-test-1");

        Assert.Equal(TransactionVoidStatus.Voided, result.Status);
        Assert.Equal(TransactionStatus.Voided, repository.Transaction!.Status);
        Assert.Equal("Duplicate sale", repository.Transaction.Reason);
        Assert.Equal(1, repository.UpdateCount);
        Assert.Equal(2, (await inventory.GetByBranchAndSkuAsync("IND01", "SKU001"))!.Quantity);

        var retry = await service.VoidAsync("TXN-APP-1", "Duplicate retry", "staff-test-1");
        Assert.Equal(TransactionVoidStatus.InvalidState, retry.Status);
        Assert.Equal(2, (await inventory.GetByBranchAndSkuAsync("IND01", "SKU001"))!.Quantity);
    }

    [Theory]
    [InlineData(StaffRole.CounterClerk, "IND01")]
    [InlineData(StaffRole.Auditor, "IND01")]
    [InlineData(StaffRole.BranchManager, "IND02")]
    public async Task VoidAsync_WhenCurrentRoleOrBranchIsNotAuthorized_DoesNotMutate(
        StaffRole currentRole,
        string currentBranch)
    {
        var repository = new FakeCounterTransactionRepository(CreatePaidTransaction());
        var staff = new FakeStaffRepository(CreateStaff(currentRole, currentBranch));
        var service = new TransactionVoidService(repository, staff);

        var result = await service.VoidAsync("TXN-APP-1", "Reason", "staff-test-1");

        Assert.Equal(TransactionVoidStatus.Forbidden, result.Status);
        Assert.Equal(TransactionStatus.Paid, repository.Transaction!.Status);
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public async Task VoidAsync_WhenStaffWasDeactivatedAfterLogin_DeniesVoid()
    {
        var repository = new FakeCounterTransactionRepository(CreatePaidTransaction());
        var currentStaff = CreateStaff(StaffRole.BranchManager, "IND01");
        currentStaff.Deactivate();
        var service = new TransactionVoidService(
            repository,
            new FakeStaffRepository(currentStaff));

        var result = await service.VoidAsync("TXN-APP-1", "Reason", currentStaff.Id);

        Assert.Equal(TransactionVoidStatus.Forbidden, result.Status);
        Assert.Equal(TransactionStatus.Paid, repository.Transaction!.Status);
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public async Task VoidAsync_WhenFormerManagerRoleChangedAfterLogin_DeniesVoid()
    {
        var repository = new FakeCounterTransactionRepository(CreatePaidTransaction());
        var currentStaff = CreateStaff(StaffRole.CounterClerk, "IND01");
        var service = new TransactionVoidService(
            repository,
            new FakeStaffRepository(currentStaff));

        var result = await service.VoidAsync("TXN-APP-1", "Reason", currentStaff.Id);

        Assert.Equal(TransactionVoidStatus.Forbidden, result.Status);
        Assert.Equal(TransactionStatus.Paid, repository.Transaction!.Status);
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public async Task VoidAsync_WhenAdministratorVoidsAcrossBranches_AllowsAndPersists()
    {
        var transaction = CreatePaidTransaction("IND02");
        var repository = new FakeCounterTransactionRepository(transaction);
        var staff = new FakeStaffRepository(CreateStaff(StaffRole.Administrator, "IND01"));
        var service = new TransactionVoidService(repository, staff);

        var result = await service.VoidAsync("TXN-APP-1", "Approved correction", "staff-test-1");

        Assert.Equal(TransactionVoidStatus.Voided, result.Status);
        Assert.Equal(TransactionStatus.Voided, repository.Transaction!.Status);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task VoidAsync_WhenStaffBranchChangedAfterLogin_UsesCurrentBranch()
    {
        var repository = new FakeCounterTransactionRepository(CreatePaidTransaction());
        var currentStaff = CreateStaff(StaffRole.BranchManager, "IND02");
        var service = new TransactionVoidService(
            repository,
            new FakeStaffRepository(currentStaff));

        var result = await service.VoidAsync("TXN-APP-1", "Reason", currentStaff.Id);

        Assert.Equal(TransactionVoidStatus.Forbidden, result.Status);
        Assert.Equal(TransactionStatus.Paid, repository.Transaction!.Status);
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public async Task VoidAsync_WhenReasonIsMissing_DoesNotMutate()
    {
        var repository = new FakeCounterTransactionRepository(CreatePaidTransaction());
        var service = new TransactionVoidService(
            repository,
            new FakeStaffRepository(CreateStaff(StaffRole.BranchManager, "IND01")));

        var result = await service.VoidAsync(
            "TXN-APP-1",
            " ",
            "staff-test-1");

        Assert.Equal(TransactionVoidStatus.InvalidReason, result.Status);
        Assert.Equal(TransactionStatus.Paid, repository.Transaction!.Status);
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public async Task VoidAsync_WhenTransactionIsOpen_DoesNotMutateOrPersist()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var inventory = new JsonInventoryRepository(directory.CreateOptions());
        await inventory.AddAsync(new InventoryStock(new BranchCode("IND01"), new Sku("SKU001"), 1));
        var transaction = CreateOpenTransaction();
        var repository = new FakeCounterTransactionRepository(transaction);
        var service = new TransactionVoidService(
            repository,
            new FakeStaffRepository(CreateStaff(StaffRole.BranchManager, "IND01")),
            inventoryRepository: inventory);

        var result = await service.VoidAsync(
            "TXN-APP-1",
            "Reason",
            "staff-test-1");

        Assert.Equal(TransactionVoidStatus.InvalidState, result.Status);
        Assert.Equal(TransactionStatus.Open, repository.Transaction!.Status);
        Assert.Null(repository.Transaction.Reason);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Equal(1, (await inventory.GetByBranchAndSkuAsync("IND01", "SKU001"))!.Quantity);
    }

    [Fact]
    public async Task VoidAsync_WhenCallerIsUnauthorized_DoesNotRestoreStock()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var inventory = new JsonInventoryRepository(directory.CreateOptions());
        await inventory.AddAsync(new InventoryStock(new BranchCode("IND01"), new Sku("SKU001"), 1));
        var repository = new FakeCounterTransactionRepository(CreatePaidTransaction());
        var service = new TransactionVoidService(
            repository,
            new FakeStaffRepository(CreateStaff(StaffRole.CounterClerk, "IND01")),
            inventoryRepository: inventory);

        var result = await service.VoidAsync("TXN-APP-1", "Not allowed", "staff-test-1");

        Assert.Equal(TransactionVoidStatus.Forbidden, result.Status);
        Assert.Equal(TransactionStatus.Paid, repository.Transaction!.Status);
        Assert.Equal(1, (await inventory.GetByBranchAndSkuAsync("IND01", "SKU001"))!.Quantity);
    }

    [Fact]
    public async Task ConcurrentVoidRetries_RestoreStockOnlyOnce()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var inventory = new JsonInventoryRepository(directory.CreateOptions());
        await inventory.AddAsync(new InventoryStock(new BranchCode("IND01"), new Sku("SKU001"), 1));
        var repository = new FakeCounterTransactionRepository(CreatePaidTransaction());
        var service = new TransactionVoidService(
            repository,
            new FakeStaffRepository(CreateStaff(StaffRole.BranchManager, "IND01")),
            inventoryRepository: inventory);

        var results = await Task.WhenAll(
            service.VoidAsync("TXN-APP-1", "Original request", "staff-test-1"),
            service.VoidAsync("TXN-APP-1", "Retried request", "staff-test-1"));

        Assert.Single(results, result => result.Status == TransactionVoidStatus.Voided);
        Assert.Single(results, result => result.Status == TransactionVoidStatus.InvalidState);
        Assert.Equal(2, (await inventory.GetByBranchAndSkuAsync("IND01", "SKU001"))!.Quantity);
        Assert.Equal(1, repository.UpdateCount);
    }

    private static CounterTransaction CreateOpenTransaction() => new(
        "TXN-APP-1",
        new BranchCode("IND01"),
        "COUNTER01",
        "CLERK01");

    private static StaffEntity CreateStaff(StaffRole role, string branchCode) => new(
        "staff-test-1",
        "Test Staff",
        "staff@example.test",
        role,
        new BranchCode(branchCode));

    private static CounterTransaction CreatePaidTransaction(string branchCode = "IND01")
    {
        var transaction = new CounterTransaction(
            "TXN-APP-1",
            new BranchCode(branchCode),
            "COUNTER01",
            "CLERK01");
        transaction.AddLineItem(new TransactionLineItem(
            "Envelope",
            "Retail",
            1,
            new Money(25m),
            referenceId: "SKU001"));
        transaction.CompletePayment(PaymentMethod.Cash);
        return transaction;
    }

    private sealed class FakeStaffRepository(StaffEntity staff) : IStaffRepository
    {
        public Task<StaffEntity?> GetByIdAsync(string staffId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<StaffEntity?>(staff.Id == staffId ? staff : null);
        }

        public Task<IReadOnlyList<StaffEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StaffEntity>>([staff]);

        public Task<IReadOnlyList<StaffEntity>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StaffEntity>>(
                staff.HomeBranchCode.Value == branchCode ? [staff] : []);

        public Task AddAsync(StaffEntity value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(StaffEntity value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeCounterTransactionRepository(CounterTransaction transaction)
        : ICounterTransactionRepository
    {
        public CounterTransaction? Transaction { get; private set; } = transaction;
        public int UpdateCount { get; private set; }

        public Task<CounterTransaction?> GetByIdAsync(
            string transactionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                string.Equals(Transaction?.Id, transactionId, StringComparison.OrdinalIgnoreCase)
                    ? Transaction
                    : null);
        }

        public Task<IReadOnlyList<CounterTransaction>> GetByBranchAsync(
            string branchCode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<CounterTransaction> results = Transaction?.BranchCode.Value == branchCode
                ? [Transaction]
                : [];
            return Task.FromResult(results);
        }

        public Task AddAsync(CounterTransaction value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(CounterTransaction value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Transaction = value;
            UpdateCount++;
            return Task.CompletedTask;
        }
    }
}
