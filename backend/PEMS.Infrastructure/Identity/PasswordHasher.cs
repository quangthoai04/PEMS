using Application.Common.Interfaces;

namespace PEMS.Infrastructure.Identity;

/// <summary>BCrypt-based password hashing (salted, adaptive work factor).</summary>
public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password must not be empty.", nameof(password));

        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            // Malformed hash etc. — treat as a failed verification, never throw.
            return false;
        }
    }
}
