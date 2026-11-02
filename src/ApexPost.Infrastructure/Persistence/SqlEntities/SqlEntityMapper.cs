using System.Text.Json;
using ApexPost.Application.Services.Auditing;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Infrastructure.Persistence.SqlEntities;

internal static class SqlEntityMapper
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);

    public static CounterTransactionRecord ToRecord(CounterTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var record = new CounterTransactionRecord
        {
            Id = transaction.Id,
            BranchCode = transaction.BranchCode,
            CounterId = transaction.CounterId,
            ClerkId = transaction.ClerkId,
            Status = transaction.Status,
            Reason = transaction.Reason,
            OccurredAtUtc = NormalizeUtc(transaction.OccurredAtUtc),
            Subtotal = transaction.Subtotal.Amount,
            Tax = transaction.Tax.Amount,
            Total = transaction.Total.Amount
        };

        record.Lines.AddRange(transaction.LineItems.Select((line, index) => new TransactionLineRecord
        {
            TransactionId = transaction.Id,
            LineNumber = index + 1,
            Description = line.Description,
            ItemType = line.ItemType,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice.Amount,
            TaxCategory = line.TaxCategory,
            ReferenceId = line.ReferenceId,
            LineTotal = line.LineTotal.Amount
        }));
        record.TaxRates.AddRange(transaction.TaxRates.Select(rate => new TransactionTaxRateRecord
        {
            TransactionId = transaction.Id,
            Category = rate.Category,
            Rate = rate.Rate
        }));
        if (transaction.PaymentMethod is { } method)
        {
            record.Payment = new PaymentRecord
            {
                TransactionId = transaction.Id,
                Method = method,
                Amount = transaction.Total.Amount,
                PaidAtUtc = NormalizeUtc(transaction.OccurredAtUtc)
            };
        }

        return record;
    }

    public static CounterTransaction ToDomain(CounterTransactionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var transaction = new CounterTransaction(
            record.Id,
            record.BranchCode,
            record.CounterId,
            record.ClerkId,
            record.TaxRates.Select(rate => new TaxRate(rate.Category, rate.Rate)),
            NormalizeUtc(record.OccurredAtUtc));

        foreach (var line in record.Lines.OrderBy(line => line.LineNumber))
        {
            if (line.LineTotal != decimal.Round(line.UnitPrice * line.Quantity, 2, MidpointRounding.AwayFromZero))
            {
                throw new InvalidDataException($"Transaction '{record.Id}' contains an inconsistent line total.");
            }
            transaction.AddLineItem(new TransactionLineItem(
                line.Description,
                line.ItemType,
                line.Quantity,
                new Money(line.UnitPrice),
                line.TaxCategory,
                line.ReferenceId));
        }

        if (transaction.Subtotal.Amount != record.Subtotal || transaction.Tax.Amount != record.Tax || transaction.Total.Amount != record.Total)
        {
            throw new InvalidDataException($"Transaction '{record.Id}' totals do not match its line items and tax rates.");
        }

        if (record.Status != TransactionStatus.Open)
        {
            if (record.Payment is null)
            {
                throw new InvalidDataException($"Paid transaction '{record.Id}' has no payment row.");
            }

            transaction.CompletePayment(record.Payment.Method);
            if (record.Status == TransactionStatus.Voided)
            {
                transaction.Void(record.Reason ?? throw new InvalidDataException("A voided transaction has no reason."));
            }
            else if (record.Status == TransactionStatus.Refunded)
            {
                transaction.Refund(record.Reason ?? throw new InvalidDataException("A refunded transaction has no reason."));
            }
            else if (record.Status != TransactionStatus.Paid)
            {
                throw new InvalidDataException($"Transaction '{record.Id}' has an unsupported status.");
            }
        }

        return transaction;
    }

    public static MoneyOrderRecord ToRecord(MoneyOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new MoneyOrderRecord
        {
            Id = order.Id,
            Amount = order.Amount.Amount,
            SenderName = order.SenderName,
            RecipientName = order.RecipientName,
            Fee = order.Fee.Amount,
            BranchCode = order.BranchCode is null ? null : new BranchCode(order.BranchCode),
            TransactionId = order.TransactionId,
            CreatedAtUtc = NormalizeUtc(order.CreatedAtUtc),
            IdentityVerification = order.IdentityVerification is { } identity
                ? new IdentityVerificationRecord
                {
                    Id = identity.Id,
                    MoneyOrderId = order.Id,
                    DocumentType = identity.DocumentType,
                    MaskedDocumentNumber = identity.MaskedDocumentNumber,
                    IsVerified = identity.IsVerified,
                    VerifiedAtUtc = NormalizeUtc(identity.VerifiedAt)
                }
                : null
        };
    }

    public static MoneyOrder ToDomain(MoneyOrderRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var order = new MoneyOrder(
            record.Id,
            new Money(record.Amount),
            record.SenderName,
            record.RecipientName,
            new Money(record.Fee));
        if (record.BranchCode is not null || record.TransactionId is not null)
        {
            if (record.BranchCode is null || record.TransactionId is null)
            {
                throw new InvalidDataException($"Money order '{record.Id}' has an incomplete transaction link.");
            }

            order.AttachToTransaction(
                record.BranchCode,
                record.TransactionId,
                record.IdentityVerification is null ? null : ToDomain(record.IdentityVerification),
                NormalizeUtc(record.CreatedAtUtc));
        }

        return order;
    }

    public static IdentityVerification ToDomain(IdentityVerificationRecord record) => new(
        record.Id,
        record.DocumentType,
        record.MaskedDocumentNumber,
        record.IsVerified,
        NormalizeUtc(record.VerifiedAtUtc));

    public static AuditEntryRecord ToRecord(AuditActionRecord record) => new()
    {
        Timestamp = NormalizeUtc(record.Timestamp),
        StaffId = record.StaffId,
        Role = record.Role,
        BranchCode = record.BranchCode,
        Controller = record.Controller,
        Action = record.Action,
        RouteValuesJson = JsonSerializer.Serialize(record.RouteValues, AuditJsonOptions),
        StatusCode = record.StatusCode,
        DurationMilliseconds = record.DurationMilliseconds,
        CorrelationId = record.CorrelationId
    };

    public static AuditActionRecord ToDomain(AuditEntryRecord record)
    {
        var routeValues = JsonSerializer.Deserialize<Dictionary<string, string?>>(record.RouteValuesJson, AuditJsonOptions)
            ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        return new AuditActionRecord(
            NormalizeUtc(record.Timestamp),
            record.StaffId,
            record.Role,
            record.BranchCode,
            record.Controller,
            record.Action,
            routeValues,
            record.StatusCode,
            record.DurationMilliseconds,
            record.CorrelationId);
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value) => value.ToUniversalTime();

    private static DateTimeOffset? NormalizeUtc(DateTimeOffset? value) =>
        value?.ToUniversalTime();
}
