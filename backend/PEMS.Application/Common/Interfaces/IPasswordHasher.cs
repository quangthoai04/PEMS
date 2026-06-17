namespace Application.Common.Interfaces;

public interface IPasswordHasher
{
    /// <summary>Hashes a plain-text password using a strong, salted algorithm (BCrypt).</summary>
    string Hash(string password);

    /// <summary>Verifies a plain-text password against a stored hash. Never throws.</summary>
    bool VerifyPassword(string password, string hash);
}
