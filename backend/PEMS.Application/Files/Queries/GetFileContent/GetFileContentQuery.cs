using MediatR;

namespace PEMS.Application.Files.Queries.GetFileContent;

/// <summary>
/// Streams a stored file's bytes by file_id (for download links + inline-image preview). Any
/// authenticated user may read it; the controller wraps the result in a FileResult.
/// </summary>
public sealed record GetFileContentQuery(ulong FileId) : IRequest<FileContentDto>;

public sealed class FileContentDto
{
    public byte[] Content { get; set; } = System.Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "file";
}
