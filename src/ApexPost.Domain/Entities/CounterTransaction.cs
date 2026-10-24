
using ApexPost.Domain.ValueObjects;
using System.Text.Json.Serialization;

namespace ApexPost.Domain.Entities;

public sealed class CounterTransaction
{
    private readonly List<TransactionLineItem> _lineItems = [];
    private readonly IReadOnlyDictionary<TaxCategory, TaxRate> _taxRates;

    public string Id { get; }
    public BranchCode BranchCode { get; }
    public string CounterId { get; }
    public string ClerkId { get; }

    [JsonInclude]
    public IReadOnlyCollection<TransactionLineItem> LineItems
    {
        get => _lineItems.AsReadOnly();
        private set
        {
            _lineItems.Clear();
            _lineItems.AddRange(value);
        }
    }

    [JsonInclude]
    public PaymentMethod? PaymentMethod { get; private set; }

    [JsonInclude]
    public TransactionStatus Status { get; private set; }

    [JsonInclude]
    public string? Reason { get; private set; }

    [JsonInclude]
    public IEnumerable<TaxRate> TaxRates => _taxRates.Values;

    public Money Subtotal => new(
        _lineItems.Sum(item => item.LineTotal.Amount));

    public Money Tax => CalculateTax();

    public Money Total => Subtotal.Add(Tax);

    public CounterTransaction(
        string id,
        BranchCode branchCode,
        string counterId,
        string clerkId,
        IEnumerable<TaxRate>? taxRates = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(counterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clerkId);

        var configuredRates = (taxRates ?? DefaultTaxRates()).ToArray();

        if (configuredRates.Any(rate => rate is null))
        {
            throw new ArgumentException(
                "Tax rates cannot contain null entries.",
                nameof(taxRates));
        }

        if (configuredRates
            .GroupBy(rate => rate.Category)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Only one tax rate can be configured per category.",
                nameof(taxRates));
        }

        _taxRates = configuredRates.ToDictionary(rate => rate.Category);

        Id = id.Trim();
        BranchCode = branchCode;
        CounterId = counterId.Trim();
        ClerkId = clerkId.Trim();
        Status = TransactionStatus.Open;
    }

    private static IEnumerable<TaxRate> DefaultTaxRates() =>
    [
        new TaxRate(TaxCategory.Standard, 0m),
        new TaxRate(TaxCategory.Reduced, 0m),
        new TaxRate(TaxCategory.ZeroRated, 0m),
        new TaxRate(TaxCategory.Exempt, 0m)
    ];

    public Money CalculateTax()
    {
        var totalTax = _lineItems.Sum(item =>
        {
            if (!_taxRates.TryGetValue(item.TaxCategory, out var rate))
            {
                throw new InvalidOperationException(
                    $"No tax rate is configured for category '{item.TaxCategory}'.");
            }

            return rate.CalculateTax(item.LineTotal.Amount);
        });

        return new Money(totalTax);
    }

    public Money CalculateTotal() => Subtotal.Add(CalculateTax());

    public void AddLineItem(TransactionLineItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Status != TransactionStatus.Open)
        {
            throw new InvalidOperationException(
                "Line items can only be added to an open transaction.");
        }

        _lineItems.Add(item);
    }

    public void CompletePayment(PaymentMethod paymentMethod)
    {
        if (Status != TransactionStatus.Open)
        {
            throw new InvalidOperationException(
                "Only open transactions can be paid.");
        }

        if (_lineItems.Count == 0)
        {
            throw new InvalidOperationException(
                "A transaction must contain at least one line item.");
        }

        if (!Enum.IsDefined(paymentMethod))
        {
            throw new ArgumentOutOfRangeException(nameof(paymentMethod));
        }

        // Validate the total before marking the transaction as paid.
        _ = Total;

        PaymentMethod = paymentMethod;
        Status = TransactionStatus.Paid;
    }

    public void Void(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status != TransactionStatus.Paid)
        {
            throw new InvalidOperationException(
                "Only paid transactions can be voided.");
        }

        Reason = reason.Trim();
        Status = TransactionStatus.Voided;
    }

    public void Refund(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status != TransactionStatus.Paid)
        {
            throw new InvalidOperationException(
                "Only paid transactions can be refunded.");
        }

        Reason = reason.Trim();
        Status = TransactionStatus.Refunded;
    }
}
