using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services.Transactions;

public sealed record TransactionRefundResult(
    TransactionRefundStatus Status,
    CounterTransaction? Transaction = null);
