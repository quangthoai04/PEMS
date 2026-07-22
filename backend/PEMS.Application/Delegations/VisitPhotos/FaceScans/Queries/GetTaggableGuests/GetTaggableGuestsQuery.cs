using System.Collections.Generic;
using MediatR;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Queries.GetTaggableGuests;

/// <summary>
/// GET /api/visit-photos/instances/{visitInstanceId}/taggable-guests — guests/support members that
/// may be tagged in this exact campus visit instance (never another instance or another delegation).
/// </summary>
public sealed class GetTaggableGuestsQuery : IRequest<List<TaggableGuestDto>>
{
    public ulong VisitInstanceId { get; }
    public GetTaggableGuestsQuery(ulong visitInstanceId) => VisitInstanceId = visitInstanceId;
}
