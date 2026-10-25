using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services.Transactions;

public sealed record CounterSaleResult(
    CounterSaleStatus Status,
    CounterTransaction? Transaction = null,
    string? ShipmentId = null);
