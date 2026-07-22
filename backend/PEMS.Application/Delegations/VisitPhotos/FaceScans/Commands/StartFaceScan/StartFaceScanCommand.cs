using MediatR;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Commands.StartFaceScan;

/// <summary>
/// POST /api/visit-photos/{visitPhotoId}/face-scans — runs Google Cloud Vision FACE_DETECTION on an
/// already-uploaded visit photo (no second upload) and stores the scan + detected face boxes for
/// manual tagging. Blocked while the same photo already has a PENDING/PROCESSING scan.
/// </summary>
public sealed class StartFaceScanCommand : IRequest<VisitPhotoFaceScanDto>
{
    public ulong VisitPhotoId { get; set; }
}
