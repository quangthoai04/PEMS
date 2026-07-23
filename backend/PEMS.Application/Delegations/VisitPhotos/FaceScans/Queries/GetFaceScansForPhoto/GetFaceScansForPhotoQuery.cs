using System.Collections.Generic;
using MediatR;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Queries.GetFaceScansForPhoto;

/// <summary>GET /api/visit-photos/{visitPhotoId}/face-scans — scan history for one photo, newest first.</summary>
public sealed class GetFaceScansForPhotoQuery : IRequest<List<VisitPhotoFaceScanDto>>
{
    public ulong VisitPhotoId { get; }
    public GetFaceScansForPhotoQuery(ulong visitPhotoId) => VisitPhotoId = visitPhotoId;
}
