using MediatR;

namespace PEMS.Application.Delegations.Queries.GetSubmittedVisitRequestFormDetail;

/// <summary>
/// Loads the read-only "what the guest submitted" snapshot for a visit request. Used by the
/// pre-approval review, the approved/waiting-host detail and the rejected detail screens.
/// Role/scope/status visibility is enforced entirely in the handler.
/// </summary>
public sealed record GetSubmittedVisitRequestFormDetailQuery(ulong VisitRequestId)
    : IRequest<SubmittedVisitRequestFormDetailDto>;
