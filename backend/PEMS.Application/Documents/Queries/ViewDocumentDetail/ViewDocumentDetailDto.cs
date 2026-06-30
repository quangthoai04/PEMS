using System;
using System.Collections.Generic;

namespace PEMS.Application.Documents.Queries.ViewDocumentDetail;

public class ViewDocumentDetailDto
{
    public DocumentDto Document { get; set; } = null!;
    public FileDto File { get; set; } = null!;
    public CampusDto? Campus { get; set; }
    public UserSummaryDto? CreatedByUser { get; set; }
    public UserSummaryDto? UpdatedByUser { get; set; }
    public UserSummaryDto? UploadedByUser { get; set; }
    public object? OwnerContext { get; set; }
}

public class DocumentDto
{
    public ulong DocumentId { get; set; }
    public ulong FileId { get; set; }
    public string OwnerType { get; set; } = null!;
    public ulong? OwnerId { get; set; }
    public ulong? CampusId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? DocumentCategory { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public ulong? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ulong? UpdatedBy { get; set; }
}

public class FileDto
{
    public ulong FileId { get; set; }
    public string StorageProvider { get; set; } = null!;
    public string? BucketName { get; set; }
    public string? ObjectKey { get; set; }
    public string OriginalFilename { get; set; } = null!;
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
    public string? ChecksumSha256 { get; set; }
    public ulong? UploadedBy { get; set; }
    public DateTime? UploadedAt { get; set; }
    public string? ExternalFileId { get; set; }
    public string? WebViewUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? FilePurpose { get; set; }
}

public class CampusDto
{
    public ulong CampusId { get; set; }
    public string CampusCode { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public class UserSummaryDto
{
    public ulong UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
}
