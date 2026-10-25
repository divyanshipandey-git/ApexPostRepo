
using ApexPost.Domain.Entities;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public sealed class IdentityVerificationTests
{
    [Fact]
    public void Constructor_ValidValues_CreatesVerification()
    {
        var verifiedAt = DateTimeOffset.UtcNow;

        var verification = new IdentityVerification(
            "IV001",
            "Aadhaar",
            "XXXX1234",
            true,
            verifiedAt);

        Assert.Equal("IV001", verification.Id);
        Assert.Equal("Aadhaar", verification.DocumentType);
        Assert.Equal("XXXX1234", verification.MaskedDocumentNumber);
        Assert.True(verification.IsVerified);
        Assert.Equal(verifiedAt, verification.VerifiedAt);
    }

    [Fact]
    public void Constructor_FailedVerification_StoresFailureResult()
    {
        var verification = new IdentityVerification(
            "IV002",
            "Passport",
            "XXXX5678",
            false,
            DateTimeOffset.UtcNow);

        Assert.False(verification.IsVerified);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("ABC1234")]
    [InlineData("XXX")]
    public void Constructor_UnmaskedOrInvalidNumber_ThrowsException(
        string documentNumber)
    {
        Assert.Throws<ArgumentException>(() =>
            new IdentityVerification(
                "IV003",
                "Passport",
                documentNumber,
                true,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_ValidValuesWithWhitespace_TrimsFields()
    {
        var verification = new IdentityVerification(
            " IV004 ",
            " Passport ",
            " XXXX1234 ",
            true,
            DateTimeOffset.UtcNow);

        Assert.Equal("IV004", verification.Id);
        Assert.Equal("Passport", verification.DocumentType);
        Assert.Equal("XXXX1234", verification.MaskedDocumentNumber);
    }
}
