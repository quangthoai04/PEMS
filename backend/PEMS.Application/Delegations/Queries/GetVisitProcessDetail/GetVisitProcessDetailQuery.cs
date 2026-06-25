using MediatR;

namespace PEMS.Application.Delegations.Queries.GetVisitProcessDetail;

/// <summary>
/// Real before-visit setup data for a campus instance (agenda + common info), scoped to anyone
/// with a relation to the instance (Host / Staff Leader of the campus / HO / Visitor owner /
/// accepted participant). Used by the VisitProcess "Trước tiếp khách" tab to render real data
/// (no mock). Writes go through the dedicated save commands (e.g. SaveVisitAgenda).
/// </summary>
public sealed record GetVisitProcessDetailQuery(
    ulong VisitRequestId,
    ulong VisitInstanceId) : IRequest<VisitProcessDetailDto>;
