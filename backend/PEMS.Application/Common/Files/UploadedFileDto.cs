namespace PEMS.Application.Common.Files;

/// <summary>
/// What <see cref="Interfaces.IFileUploadService"/> returns after a successful business upload. The
/// business handler stores <see cref="FileId"/> / <see cref="FileUrl"/> against its own entity
/// (e.g. <c>users.avatar_url</c>, <c>gallery_images.file_id</c>).
/// </summary>
public sealed class UploadedFileDto
{
    public long FileId { get; init; }

    /// <summary>Backend proxy URL — <c>/api/files/{fileId}/content</c>.</summary>
    public string FileUrl { get; init; } = default!;

    public string StorageProvider { get; init; } = "GOOGLE_DRIVE";
    public string ExternalFileId { get; init; } = default!;
    public string? WebViewUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string MimeType { get; init; } = default!;
    public long FileSize { get; init; }
    public string ChecksumSha256 { get; init; } = default!;
    public string ObjectKey { get; init; } = default!;
}
