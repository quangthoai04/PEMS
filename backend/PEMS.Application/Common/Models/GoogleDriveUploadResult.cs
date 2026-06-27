namespace PEMS.Application.Common.Models;

/// <summary>
/// Outcome of uploading a file to Google Drive. Mirrors the bits of the Drive API
/// <c>files.create</c> response that we persist into the <c>files</c> table.
/// </summary>
public sealed class GoogleDriveUploadResult
{
    /// <summary>Always <c>GOOGLE_DRIVE</c> — stored verbatim in <c>files.storage_provider</c>.</summary>
    public string StorageProvider { get; init; } = "GOOGLE_DRIVE";

    /// <summary>Google Drive file id (<c>files.external_file_id</c>).</summary>
    public string ExternalFileId { get; init; } = default!;

    /// <summary>Human-facing Drive link (<c>webViewLink</c>).</summary>
    public string? WebViewUrl { get; init; }

    /// <summary>Direct content link (<c>webContentLink</c>) — requires an authorized request to read.</summary>
    public string? DownloadUrl { get; init; }

    /// <summary>Thumbnail link if Google returned one.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>Size in bytes as reported by Drive (falls back to the uploaded length).</summary>
    public long FileSize { get; init; }
}
