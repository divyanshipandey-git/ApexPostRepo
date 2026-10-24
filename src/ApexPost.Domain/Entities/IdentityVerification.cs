
namespace ApexPost.Domain.Entities;

public sealed class IdentityVerification
{
    public string Id { get; }
    public string DocumentType { get; }
    public string MaskedDocumentNumber { get; }
    public bool IsVerified { get; }
    public DateTimeOffset VerifiedAt { get; }

    public IdentityVerification(
        string id,
        string documentType,
        string maskedDocumentNumber,
        bool isVerified,
        DateTimeOffset verifiedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(maskedDocumentNumber);

        if (!IsValidMaskedNumber(maskedDocumentNumber))
        {
            throw new ArgumentException(
                "Document number must be masked, with only its last four characters visible.",
                nameof(maskedDocumentNumber));
        }

        Id = id.Trim();
        DocumentType = documentType.Trim();
        MaskedDocumentNumber = maskedDocumentNumber.Trim();
        IsVerified = isVerified;
        VerifiedAt = verifiedAt;
    }

    private static bool IsValidMaskedNumber(string value)
    {
        var masked = value.Trim();

        return masked.Length == 8
            && masked.StartsWith("XXXX", StringComparison.OrdinalIgnoreCase)
            && masked[4..].All(char.IsAsciiLetterOrDigit);
    }
}
