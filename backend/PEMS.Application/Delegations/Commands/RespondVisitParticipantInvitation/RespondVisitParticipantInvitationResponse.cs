namespace PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;

public sealed record RespondVisitParticipantInvitationResponse(
    ulong ParticipantId,
    string Status,
    string Message);
