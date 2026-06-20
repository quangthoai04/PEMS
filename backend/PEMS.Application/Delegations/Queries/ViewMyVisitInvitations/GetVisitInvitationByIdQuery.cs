using MediatR;

namespace PEMS.Application.Delegations.Queries.ViewMyVisitInvitations;

/// <summary>
/// UC-27: a single invitation by participant id, for the invitation-detail screen.
/// The handler enforces ownership (only the invited user may read it).
/// </summary>
public sealed record GetVisitInvitationByIdQuery(ulong ParticipantId) : IRequest<VisitInvitationDto>;
