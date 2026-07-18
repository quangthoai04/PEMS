using MediatR;

namespace PEMS.Application.Delegations.VisitPhotos.Commands.RemoveVisitPhoto;

/// <summary>
/// Soft-deletes one visit photo (status REMOVED + removed_at/removed_by/removal_reason). Only the
/// Student who uploaded it — and who still has ACCEPTED participation in the photo's instance —
/// may remove it. The Drive binary and its <c>files</c> row are kept (soft-delete policy).
/// </summary>
public sealed class RemoveVisitPhotoCommand : IRequest<Unit>
{
    public ulong VisitPhotoId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
