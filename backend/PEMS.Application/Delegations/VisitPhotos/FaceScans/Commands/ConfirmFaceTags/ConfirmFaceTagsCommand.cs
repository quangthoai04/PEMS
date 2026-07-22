using System.Collections.Generic;
using MediatR;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Commands.ConfirmFaceTags;

/// <summary>
/// POST /api/visit-photos/face-scans/{faceScanId}/confirm — batch-confirms every detected face of a
/// SUCCEEDED scan: each face is either tagged to a guest of the exact visit instance or marked
/// ignored. Creates the canonical photo_face_tags rows and flips the scan to CONFIRMED.
/// </summary>
public sealed class ConfirmFaceTagsCommand : IRequest<VisitPhotoFaceScanDto>
{
    public ulong FaceScanId { get; set; }
    public uint RowVersion { get; set; }
    public List<ConfirmFaceTagItem> Faces { get; set; } = new();
}

public sealed class ConfirmFaceTagItem
{
    public ulong FaceDetectionId { get; set; }
    public ulong? GuestMemberId { get; set; }
    public bool Ignored { get; set; }
}
