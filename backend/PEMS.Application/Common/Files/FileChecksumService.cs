using System.Security.Cryptography;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Common.Files;

/// <inheritdoc cref="IFileChecksumService"/>
public sealed class FileChecksumService : IFileChecksumService
{
    public async Task<string> ComputeSha256HexAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);

        if (stream.CanSeek)
            stream.Position = 0;

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
