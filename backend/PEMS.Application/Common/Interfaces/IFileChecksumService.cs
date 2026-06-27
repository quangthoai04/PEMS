namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Computes the SHA-256 checksum stored in <c>files.checksum_sha256</c>. Always lowercase hex,
/// 64 characters. The checksum is computed from the file bytes only — never from the filename,
/// external id or any client-supplied value.
/// </summary>
public interface IFileChecksumService
{
    Task<string> ComputeSha256HexAsync(Stream stream, CancellationToken cancellationToken = default);
}
