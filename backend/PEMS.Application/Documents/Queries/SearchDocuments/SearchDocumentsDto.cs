using System;

namespace PEMS.Application.Documents.Queries.SearchDocuments;

public sealed class SearchDocumentsDto
{
    public ulong DocumentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? DocumentCategory { get; init; }
    public string Status { get; init; } = string.Empty;
    public string OwnerType { get; init; } = string.Empty;
    public ulong? OwnerId { get; init; }
    public string? OwnerDisplayName { get; init; }
    public ulong? CampusId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public ulong? FileId { get; init; }
    public string? OriginalFilename { get; init; }
    public string? MimeType { get; init; }
    public long? FileSize { get; init; }
    public string? StorageProvider { get; init; }
    public string? DownloadUrl { get; init; }
    public string? WebViewUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
}