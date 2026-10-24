using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Notifications;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using StaffEntity = ApexPost.Domain.Entities.Staff;

namespace ApexPost.Application.Services.Transactions;

public sealed class TransactionVoidService
{
    private readonly ICounterTransactionRepository _repository;
    private readonly IStaffRepository _staffRepository;
    private readonly INotificationService? _notifications;

    public TransactionVoidService(
        ICounterTransactionRepository repository,
        IStaffRepository staffRepository,
        INotificationService? notifications = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(staffRepository);
        _repository = repository;
        _staffRepository = staffRepository;
        _notifications = notifications;
    }

    public async Task<TransactionVoidResult> GetAccessibleAsync(
        string transactionId,
        string? staffId,
        CancellationToken cancellationToken = default)
    {
        var staff = await GetAuthorizedStaffAsync(staffId, cancellationToken);
        if (staff is null)
        {
            return new TransactionVoidResult(TransactionVoidStatus.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return new TransactionVoidResult(TransactionVoidStatus.InvalidTransactionId);
        }

        var transaction = await _repository.GetByIdAsync(transactionId, cancellationToken);
        if (transaction is null)
        {
            return new TransactionVoidResult(TransactionVoidStatus.NotFound);
        }

        if (!CanAccessBranch(staff, transaction.BranchCode))
        {
            return new TransactionVoidResult(TransactionVoidStatus.Forbidden);
        }

        return new TransactionVoidResult(TransactionVoidStatus.Available, transaction);
    }

    public async Task<IReadOnlyList<CounterTransaction>?> GetBranchTransactionsAsync(
        string? staffId,
        CancellationToken cancellationToken = default)
    {
        var staff = await GetAuthorizedStaffAsync(staffId, cancellationToken);
        if (staff is null)
        {
            return null;
        }

        try
        {
            var branch = staff.HomeBranchCode;
            return await _repository.GetByBranchAsync(branch.Value, cancellationToken);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public async Task<TransactionVoidResult> VoidAsync(
        string transactionId,
        string? reason,
        string? staffId,
        CancellationToken cancellationToken = default)
    {
        var accessible = await GetAccessibleAsync(
            transactionId,
            staffId,
            cancellationToken);
        if (accessible.Status != TransactionVoidStatus.Available)
        {
            return accessible;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new TransactionVoidResult(TransactionVoidStatus.InvalidReason, accessible.Transaction);
        }

        var transaction = accessible.Transaction!;
        try
        {
            transaction.Void(reason);
        }
        catch (InvalidOperationException)
        {
            return new TransactionVoidResult(TransactionVoidStatus.InvalidState, transaction);
        }
        catch (ArgumentException)
        {
            return new TransactionVoidResult(TransactionVoidStatus.InvalidReason, transaction);
        }

        await _repository.UpdateAsync(transaction, cancellationToken);
        if (_notifications is not null)
        {
            await _notifications.NotifyTransactionVoidAsync(
                transaction.BranchCode.Value,
                transaction.Id,
                transaction.Reason!,
                CancellationToken.None);
        }

        return new TransactionVoidResult(TransactionVoidStatus.Voided, transaction);
    }

    private async Task<StaffEntity?> GetAuthorizedStaffAsync(
        string? staffId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(staffId))
        {
            return null;
        }

        var staff = await _staffRepository.GetByIdAsync(staffId, cancellationToken);
        return staff is { IsActive: true, Role: StaffRole.BranchManager or StaffRole.Administrator }
            ? staff
            : null;
    }

    private static bool CanAccessBranch(StaffEntity staff, BranchCode transactionBranchCode)
    {
        if (staff.Role == StaffRole.Administrator)
        {
            return true;
        }

        if (staff.Role != StaffRole.BranchManager)
        {
            return false;
        }

        return staff.HomeBranchCode == transactionBranchCode;
    }
}
