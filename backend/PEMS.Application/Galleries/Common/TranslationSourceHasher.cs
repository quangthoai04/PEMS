using System.Security.Cryptography;
using System.Text;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// SHA-256 (UTF-8 → lowercase hex, 64 chars) of an already-normalized Vietnamese source string.
/// Stored in <c>translation_source_hash</c> so a later save can tell whether the source actually
/// changed (hash match + READY + EN present → skip the Translation API entirely).
/// </summary>
public static class TranslationSourceHasher
{
    public static string ComputeHash(string normalizedSource)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSource ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
