using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Documents;

using PEMS.Application.Common;
namespace PEMS.Infrastructure.FileStorage;

/// <summary>
/// Disk-backed <see cref="IFileStorageService"/>. Files are written under
/// <c>FileStorage:LocalRoot</c> (default: &lt;content-root&gt;/App_Data/uploads) using an opaque
/// object key, so original filenames never touch the filesystem path. <see cref="OpenReadAsync"/>
/// reads LOCAL files from disk and falls back to fetching a remote provider's download URL (e.g.
/// Google Drive) so the email MIME builder can stream any file by its <c>file_id</c>.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _root;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ILogger<LocalFileStorageService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
        var configured = configuration["FileStorage:LocalRoot"];
        // Resolved once, so the containment check in OpenReadAsync compares two absolute paths rather
        // than a mix of relative and absolute ones.
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads")
            : configured);
    }

    /// <summary>
    /// True when an already-resolved absolute path sits inside the storage root. Compared with a trailing
    /// separator so a sibling directory whose name merely starts with the root's ("…/uploads-old") is not
    /// mistaken for a child of it.
    /// </summary>
    private bool IsUnderRoot(string fullPath)
    {
        var root = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<StoredFileInfo> SaveAsync(
        Stream content, string originalFilename, string? contentType, string? purpose,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFilename ?? string.Empty);
        var folder = Sanitize(purpose) ?? "misc";
        // Opaque key: <purpose>/<yyyy>/<MM>/<guid><ext> — date partitioning keeps folders small.
        var now = VietnamTime.Now();
        var objectKey = $"{folder}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{ext}".Replace('\\', '/');

        var fullPath = Path.Combine(_root, objectKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        long size;
        string checksum;
        using (var sha = SHA256.Create())
        await using (var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        await using (var crypto = new CryptoStream(file, sha, CryptoStreamMode.Write))
        {
            await content.CopyToAsync(crypto, cancellationToken);
            crypto.FlushFinalBlock();
            size = file.Length;
            checksum = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }

        return new StoredFileInfo("LOCAL", objectKey, size, checksum);
    }

    public async Task<Stream?> OpenReadAsync(UploadedFile file, CancellationToken cancellationToken = default)
    {
        if (file is null) return null;

        var isLocal = string.Equals(file.StorageProvider, "LOCAL", StringComparison.OrdinalIgnoreCase);
        if (isLocal && !string.IsNullOrWhiteSpace(file.ObjectKey))
        {
            // The key is generated here (an opaque GUID under a purpose/date folder) and a caller only
            // ever supplies a numeric file id, so a traversal sequence should not be able to reach this
            // point. The containment check runs anyway: a stored key is data, and data that decides which
            // path to open is worth verifying rather than trusting. A key that resolves outside the
            // storage root is treated as a missing file — never opened, and never echoed back.
            var fullPath = Path.GetFullPath(
                Path.Combine(_root, file.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));

            if (!IsUnderRoot(fullPath))
            {
                _logger.LogError(
                    "Rejected a stored object key that resolves outside the storage root: fileId={FileId}",
                    file.FileId);
                return null;
            }

            if (File.Exists(fullPath))
                return File.OpenRead(fullPath);
            _logger.LogWarning("Local file missing on disk: fileId={FileId} objectKey={ObjectKey}", file.FileId, file.ObjectKey);
            return null;
        }

        if (string.Equals(file.StorageProvider, "GOOGLE_DRIVE", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(file.ExternalFileId)) return null;
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var googleDrive = scope.ServiceProvider.GetRequiredService<IGoogleDriveStorageService>();
                return await googleDrive.DownloadAsync(file.ExternalFileId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Google Drive file bytes: fileId={FileId} externalFileId={ExternalFileId}", file.FileId, file.ExternalFileId);
                return null;
            }
        }

        // Remote provider (e.g. other than Google Drive): fetch the file bytes over HTTP.
        var url = !string.IsNullOrWhiteSpace(file.DownloadUrl) ? file.DownloadUrl : file.WebViewUrl;
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var client = _httpClientFactory.CreateClient();
            var bytes = await client.GetByteArrayAsync(url, cancellationToken);
            return new MemoryStream(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch remote file bytes: fileId={FileId} url={Url}", file.FileId, url);
            return null;
        }
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = new string(value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}
