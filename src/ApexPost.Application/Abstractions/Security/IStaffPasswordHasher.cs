namespace ApexPost.Application.Abstractions.Security;

public interface IStaffPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string? encodedHash);
}
