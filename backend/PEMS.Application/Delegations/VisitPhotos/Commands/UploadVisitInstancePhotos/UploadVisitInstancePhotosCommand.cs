using MediatR;

namespace PEMS.Application.Delegations.VisitPhotos.Commands.UploadVisitInstancePhotos;

public sealed record UploadVisitInstancePhotoFile(byte[] Content, string FileName, string ContentType);

/// <summary>
/// Student (ACCEPTED participant of the exact campus instance) uploads delegation photos. The
/// backend resolves <c>visit_request_id</c> and <c>campus_code</c> from the instance itself —
/// relation IDs supplied by the frontend are never trusted.
/// </summary>
public sealed class UploadVisitInstancePhotosCommand : IRequest<UploadVisitInstancePhotosResponse>
{
    public ulong VisitInstanceId { get; set; }
    public List<UploadVisitInstancePhotoFile> Files { get; set; } = new();
}
