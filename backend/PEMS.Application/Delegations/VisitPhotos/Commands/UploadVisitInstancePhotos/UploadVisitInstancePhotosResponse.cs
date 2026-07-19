namespace PEMS.Application.Delegations.VisitPhotos.Commands.UploadVisitInstancePhotos;

public sealed class UploadedVisitPhotoDto
{
    public ulong VisitPhotoId { get; init; }
    public string FileName { get; init; } = string.Empty;

    /// <summary>Authorized proxy URL (<c>/api/files/{fileId}/content</c>) — no Drive id exposed.</summary>
    public string Url { get; init; } = string.Empty;
}

public sealed class UploadVisitInstancePhotosResponse
{
    public IReadOnlyList<UploadedVisitPhotoDto> Photos { get; init; } = Array.Empty<UploadedVisitPhotoDto>();
}
