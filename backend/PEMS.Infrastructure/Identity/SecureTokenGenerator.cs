using System.Security.Cryptography;
using System.Text;

namespace PEMS.Infrastructure.Identity;

/// <summary>
/// Helpers for refresh tokens / OTP codes: cryptographically-random generation
/// plus a deterministic SHA-256 hash used for storage (so raw values never hit the DB).
/// </summary>
internal static class SecureTokenGenerator
{
    /// <summary>Generates a URL-safe random opaque token (used for refresh tokens).</summary>
    public static string GenerateOpaqueToken(int byteLength = 48)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }

    /// <summary>Generates a numeric OTP code of the given length (default 6 digits).</summary>
    public static string GenerateNumericCode(int digits = 6)
    {
        var max = (int)Math.Pow(10, digits);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString(new string('0', digits));
    }

    /// <summary>SHA-256 hex hash of a raw token. Stable for lookups by hash.</summary>
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
