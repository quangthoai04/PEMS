using MediatR;

namespace PEMS.Application.Files.Commands.UploadFile;

/// <summary>
/// Uploads a file's bytes into storage and registers a row in <c>files</c>. Returns the file_id +
/// URLs so the caller can attach it to an email draft (as ATTACHMENT or INLINE_IMAGE) or embed it.
/// The controller reads the multipart IFormFile into <see cref="Content"/>.
/// </summary>
public sealed class UploadFileCommand : IRequest<UploadedFileDto>
{
    public byte[] Content { get; set; } = System.Array.Empty<byte>();
    public string FileName { get; set; } = "file";
    public string? ContentType { get; set; }
    /// <summary>Logical bucket/purpose, e.g. EMAIL_ATTACHMENT / EMAIL_INLINE (also used as the storage folder).</summary>
    public string? Purpose { get; set; }
}

public sealed class UploadedFileDto
{
    public ulong FileId { get; set; }
    public string OriginalFilename { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
    public string? DownloadUrl { get; set; }
    public string? WebViewUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
}
