using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services.Transactions;

public sealed record CounterSaleOptions(
    IReadOnlyList<Product> Products,
    IReadOnlyList<Counter> Counters);
