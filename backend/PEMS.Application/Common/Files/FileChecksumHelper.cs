using System.Security.Cryptography;

namespace PEMS.Application.Common.Files;

/// <summary>
/// Computes the SHA-256 checksum of a file's binary content as a 64-char lowercase hex string.
/// Used to populate <c>files.checksum_sha256</c> for integrity / audit / dedupe. The hash is taken
/// from the raw bytes ONLY — never from the filename, storage id, or any URL.
/// </summary>
public static class FileChecksumHelper
{
    /// <summary>Hashes an in-memory buffer (preferred for small files already read into memory).</summary>
    public static string ComputeSha256Hex(ReadOnlySpan<byte> content)
        => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    /// <summary>
    /// Hashes a stream's content. Rewinds to the start before and after hashing when the stream
    /// is seekable, so the caller can immediately re-read it (e.g. to upload the same stream).
    /// </summary>
    public static async Task<string> ComputeSha256HexAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);

        if (stream.CanSeek)
            stream.Position = 0;

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
