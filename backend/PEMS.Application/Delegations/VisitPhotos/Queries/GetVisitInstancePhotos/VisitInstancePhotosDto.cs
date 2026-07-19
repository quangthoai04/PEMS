namespace PEMS.Application.Delegations.VisitPhotos.Queries.GetVisitInstancePhotos;

public sealed class VisitInstancePhotoItemDto
{
    public ulong VisitPhotoId { get; init; }
    public string FileName { get; init; } = string.Empty;

    /// <summary>Authorized proxy URL (<c>/api/files/{fileId}/content</c>) — no Drive id exposed.</summary>
    public string Url { get; init; } = string.Empty;

    public string? Caption { get; init; }
    public DateTime UploadedAt { get; init; }
    public string UploadedByName { get; init; } = string.Empty;
    public bool UploadedByMe { get; init; }

    /// <summary>Soft-delete allowed: own photo + upload window still open.</summary>
    public bool CanRemove { get; init; }
}

public sealed class VisitInstancePhotosDto
{
    public ulong VisitInstanceId { get; init; }
    public string DelegationName { get; init; } = string.Empty;
    public string? CampusName { get; init; }
    public string? FolderName { get; init; }

    /// <summary>Backend-provided Drive link of the request folder (never assembled client-side).</summary>
    public string? FolderWebViewUrl { get; init; }

    public bool CanUpload { get; init; }
    public IReadOnlyList<VisitInstancePhotoItemDto> Photos { get; init; } = Array.Empty<VisitInstancePhotoItemDto>();
}
