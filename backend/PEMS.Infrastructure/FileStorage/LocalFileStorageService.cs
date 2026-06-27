using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Documents;

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
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads")
            : configured;
    }

    public async Task<StoredFileInfo> SaveAsync(
        Stream content, string originalFilename, string? contentType, string? purpose,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFilename ?? string.Empty);
        var folder = Sanitize(purpose) ?? "misc";
        // Opaque key: <purpose>/<yyyy>/<MM>/<guid><ext> — date partitioning keeps folders small.
        var now = DateTime.Now;
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
            var fullPath = Path.Combine(_root, file.ObjectKey.Replace('/', Path.DirectorySeparatorChar));
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
