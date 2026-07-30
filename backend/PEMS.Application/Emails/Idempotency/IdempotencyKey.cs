using System;
using System.Security.Cryptography;
using System.Text;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Idempotency;

/// <summary>
/// Validates and hashes the client's <c>Idempotency-Key</c>.
///
/// <para>
/// The value is opaque and case-sensitive: the server never parses it, never derives it, and never
/// infers anything from it. It is a name for one attempt, and the only thing the server does with it is
/// compare it to names it has seen.
/// </para>
/// </summary>
public static class IdempotencyKey
{
    /// <summary>The header the six send routes read.</summary>
    public const string HeaderName = "Idempotency-Key";

    /// <summary>Short enough to be a typo or a truncation rather than a UUID.</summary>
    public const int MinLength = 8;

    /// <summary>Long enough for any sane identifier; anything longer is a payload in disguise.</summary>
    public const int MaxLength = 200;

    /// <summary>
    /// Accepts printable US-ASCII excluding space (0x21–0x7E). Chosen for what it EXCLUDES: CR, LF, NUL
    /// and every other control character cannot appear, so a key can never be smuggled into a log line
    /// or a header. A UUID, a ULID and a base64url token all pass unchanged.
    /// </summary>
    public static bool IsWellFormed(string? key)
    {
        if (key is null || key.Length < MinLength || key.Length > MaxLength) return false;

        foreach (var c in key)
            if (c < '!' || c > '~') return false;

        return true;
    }

    /// <summary>
    /// Returns the key's hash, or throws the stable failure the client must handle. There is no third
    /// option: a send route with no usable key is refused, never quietly sent down a legacy path.
    /// </summary>
    public static string RequireHash(string? key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ValidationException(
                "Yêu cầu gửi thiếu Idempotency-Key. Tải lại trang rồi thử lại.",
                EmailErrorCodes.IdempotencyKeyRequired);

        if (!IsWellFormed(key))
            throw new ValidationException(
                "Idempotency-Key không hợp lệ.",
                EmailErrorCodes.IdempotencyKeyInvalid);

        return Hash(key);
    }

    /// <summary>Lower-case hex SHA-256. Only this reaches the database — never the key itself.</summary>
    public static string Hash(string key)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
}
