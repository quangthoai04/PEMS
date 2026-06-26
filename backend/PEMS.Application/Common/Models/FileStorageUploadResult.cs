namespace PEMS.Application.Common.Models;

/// <summary>
/// Provider-agnostic result of a successful upload. Maps onto the columns of the
/// <c>files</c> table so a handler can persist the metadata directly.
/// </summary>
public sealed class FileStorageUploadResult
{
    public string StorageProvider { get; set; } = "GOOGLE_DRIVE";
    public string ExternalFileId { get; set; } = default!;
    public string? WebViewUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string ObjectKey { get; set; } = default!;
    public string MimeType { get; set; } = default!;
    public long FileSize { get; set; }
}
