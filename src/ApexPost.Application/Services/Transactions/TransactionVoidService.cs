using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Notifications;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using System.Collections.Concurrent;
using StaffEntity = ApexPost.Domain.Entities.Staff;

namespace ApexPost.Application.Services.Transactions;

public sealed class TransactionVoidService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ReversalGates =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ICounterTransactionRepository _repository;
    private readonly IStaffRepository _staffRepository;
    private readonly INotificationService? _notifications;
    private readonly IInventoryRepository? _inventoryRepository;

    public TransactionVoidService(
        ICounterTransactionRepository repository,
        IStaffRepository staffRepository,
        INotificationService? notifications = null,
        IInventoryRepository? inventoryRepository = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(staffRepository);
        _repository = repository;
        _staffRepository = staffRepository;
        _notifications = notifications;
        _inventoryRepository = inventoryRepository;
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
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return new TransactionVoidResult(TransactionVoidStatus.InvalidTransactionId);
        }

        using var reversalGate = await AcquireReversalGateAsync(transactionId, cancellationToken);
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

        await RestoreSoldRetailStockAsync(transaction, cancellationToken);
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

    internal async Task RestoreSoldRetailStockAsync(
        CounterTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (_inventoryRepository is null)
        {
            return;
        }

        var quantities = transaction.LineItems
            .Where(item => item.ItemType == "Retail" && !string.IsNullOrWhiteSpace(item.ReferenceId))
            .GroupBy(item => item.ReferenceId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity), StringComparer.OrdinalIgnoreCase);

        if (quantities.Count > 0)
        {
            await _inventoryRepository.RestoreReversedRetailStockAsync(
                transaction.Id,
                transaction.BranchCode.Value,
                quantities,
                cancellationToken);
        }
    }

    internal static async Task<IDisposable> AcquireReversalGateAsync(
        string transactionId,
        CancellationToken cancellationToken)
    {
        var gate = ReversalGates.GetOrAdd(transactionId.Trim(), static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new SemaphoreLease(gate);
    }

    private sealed class SemaphoreLease(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
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
