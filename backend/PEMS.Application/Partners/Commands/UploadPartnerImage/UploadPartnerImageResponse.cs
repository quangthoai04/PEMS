namespace PEMS.Application.Partners.Commands.UploadPartnerImage;

public sealed class UploadPartnerImageResponse
{
    public long    FileId       { get; init; }
    public string  FileUrl      { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string? MimeType     { get; init; }
    public long    FileSize     { get; init; }
}
