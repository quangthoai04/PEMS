using MediatR;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Queries.GetFaceScanDetail;

/// <summary>GET /api/visit-photos/face-scans/{faceScanId} — one scan with its detected face boxes.</summary>
public sealed class GetFaceScanDetailQuery : IRequest<VisitPhotoFaceScanDto>
{
    public ulong FaceScanId { get; }
    public GetFaceScanDetailQuery(ulong faceScanId) => FaceScanId = faceScanId;
}
