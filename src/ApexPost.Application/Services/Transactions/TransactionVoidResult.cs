using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services.Transactions;

public sealed record TransactionVoidResult(
    TransactionVoidStatus Status,
    CounterTransaction? Transaction = null);
