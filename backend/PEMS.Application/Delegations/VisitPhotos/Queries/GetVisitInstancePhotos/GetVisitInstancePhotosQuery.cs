using MediatR;

namespace PEMS.Application.Delegations.VisitPhotos.Queries.GetVisitInstancePhotos;

/// <summary>Photo detail for one campus instance, scoped to the calling ACCEPTED Student.</summary>
public sealed record GetVisitInstancePhotosQuery(ulong VisitInstanceId) : IRequest<VisitInstancePhotosDto>;
