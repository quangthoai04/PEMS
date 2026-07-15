using MediatR;
using PEMS.Application.Delegations.Services.VisitFormRead;

namespace PEMS.Application.Delegations.Queries.GetVisitRequestFormV2;

/// <summary>
/// Reference PR-3 read path that returns the fully per-campus resolved form via the central
/// <see cref="IVisitFormReadService"/>. Gated by the <c>PerCampusFormV2</c> feature flag: when the
/// flag is OFF the endpoint behaves as if it does not exist (404), so no v1 behaviour changes.
/// </summary>
public sealed record GetVisitRequestFormV2Query(ulong VisitRequestId) : IRequest<ResolvedVisitFormDto>;
