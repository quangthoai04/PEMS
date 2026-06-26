namespace PEMS.Application.Files.Queries.GetFileContent;

/// <summary>The readable stream + headers needed to write a file response.</summary>
public sealed class FileContentResult
{
    public Stream Content { get; set; } = default!;
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "file";
}
