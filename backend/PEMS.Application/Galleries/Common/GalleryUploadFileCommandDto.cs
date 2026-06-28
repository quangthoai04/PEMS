namespace PEMS.Application.Galleries.Common;

/// <summary>
/// One uploaded media file buffered by the controller (bytes + metadata) so the handler can hand it to
/// the shared <c>IFileUploadService</c> and sniff its type. Caption / alt text are optional per-file.
/// </summary>
public sealed record GalleryUploadFileCommandDto(
    byte[] Content,
    string FileName,
    string? ContentType,
    long FileSize,
    string? Caption,
    string? AltText);
