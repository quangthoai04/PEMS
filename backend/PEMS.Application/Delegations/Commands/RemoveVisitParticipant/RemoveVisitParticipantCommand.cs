using MediatR;

namespace PEMS.Application.Delegations.Commands.RemoveVisitParticipant;

/// <summary>
/// Host withdraws a participant they invited. Allowed only while the invitation is still pending
/// (status INVITED) — the host can never remove the main host, and removing an already
/// ACCEPTED/ASSIGNED participant requires a dedicated confirmed flow (out of scope here).
/// </summary>
public sealed record RemoveVisitParticipantCommand(ulong VisitInstanceId, ulong ParticipantId)
    : IRequest<RemoveVisitParticipantResponse>;

public sealed record RemoveVisitParticipantResponse(ulong ParticipantId, string Status, string Message);
