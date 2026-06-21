using MediatR;

namespace PEMS.Application.Delegations.Queries.GetVisitInvitationDetail;

public sealed record GetVisitInvitationDetailQuery(ulong ParticipantId) : IRequest<VisitInvitationDetailDto>;
