using System.Globalization;
using System.Security.Cryptography;
using ApexPost.Application.Abstractions.Security;

namespace ApexPost.Infrastructure.Security;

public sealed class Pbkdf2StaffPasswordHasher : IStaffPasswordHasher
{
    private const string Algorithm = "pbkdf2-sha256";
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int MinimumAcceptedIterations = 100_000;
    private const int MaximumAcceptedIterations = 1_000_000;

    private static readonly byte[] DummySalt = new byte[SaltSize];
    private static readonly byte[] DummyHash = Rfc2898DeriveBytes.Pbkdf2(
        "ApexPost dummy credential",
        DummySalt,
        Iterations,
        HashAlgorithmName.SHA256,
        KeySize);

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return string.Join(
            '$',
            Algorithm,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool VerifyPassword(string password, string? encodedHash)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (!TryParse(encodedHash, out var iterations, out var salt, out var expectedHash))
        {
            var dummy = Rfc2898DeriveBytes.Pbkdf2(
                password,
                DummySalt,
                Iterations,
                HashAlgorithmName.SHA256,
                KeySize);
            CryptographicOperations.FixedTimeEquals(dummy, DummyHash);
            CryptographicOperations.ZeroMemory(dummy);
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        try
        {
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actualHash);
        }
    }

    private static bool TryParse(
        string? encodedHash,
        out int iterations,
        out byte[] salt,
        out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];

        var parts = encodedHash?.Split('$');
        if (parts is not { Length: 4 }
            || !string.Equals(parts[0], Algorithm, StringComparison.Ordinal)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
            || iterations is < MinimumAcceptedIterations or > MaximumAcceptedIterations)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
            return salt.Length == SaltSize && hash.Length == KeySize;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
