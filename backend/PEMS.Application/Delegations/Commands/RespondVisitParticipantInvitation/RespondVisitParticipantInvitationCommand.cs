using MediatR;

namespace PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;

/// <summary>
/// UC-27 Confirm Participation. A non-host invitee (IC_SUPPORT / DEPT_SUPPORT / STUDENT)
/// responds to their own participation invitation on a visit instance.
///   • <paramref name="Accept"/> = true  → status ACCEPTED, responded_at = now; an optional
///     <paramref name="Note"/> is stored on visit_participants.note (same column DECLINE uses —
///     never both set, since only one branch runs per response).
///   • <paramref name="Accept"/> = false → status DECLINED, responded_at = now; a
///     <paramref name="DeclineReason"/> is mandatory and stored on visit_participants.note.
/// This lives on the dedicated invitation-detail screen, NOT on the "Đơn mời tham dự" tab
/// (which only lists already-ACCEPTED invitations and is read-only).
/// </summary>
public sealed record RespondVisitParticipantInvitationCommand(
    ulong ParticipantId,
    bool Accept,
    string? DeclineReason,
    string? Note = null)
    : IRequest<RespondVisitParticipantInvitationResponse>;
