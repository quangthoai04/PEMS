using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.EmailActions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// The ONE implementation of "an invitee answers their invitation".
///
/// <para>
/// There used to be two. <c>RespondVisitParticipantInvitation</c> checked ownership, the participant
/// role, the current status and the visit lifecycle before writing; the department screens went through
/// <c>DepartmentReceptionTasks.AcceptInvitation</c> / <c>DeclineInvitation</c>, which checked none of
/// them — both status guards were commented out, and the participant was loaded by id alone, so posting
/// somebody else's participant id accepted on their behalf. The same business action, INVITED →
/// ACCEPTED, obeyed different rules depending on which screen the button was on.
/// </para>
/// <para>
/// So the transition lives here and every entry point calls it. What each caller still owns is what is
/// genuinely theirs: the transaction and the account lock, any pre-check specific to that screen (the
/// department calendar's double-booking check), <c>SaveChangesAsync</c>, and the notification it sends
/// afterwards. What none of them may do any more is write <c>participant.Status</c> themselves.
/// </para>
/// </summary>
public static class VisitInvitationResponse
{
    /// <summary>
    /// Whether the visit is in a state where an invitation can still be answered at all.
    ///
    /// <para>
    /// BEFORE_VISIT and nothing else, and it cannot be otherwise: inviting is a preparation action
    /// (<see cref="VisitPreparationGate"/>), so an invitation can only ever have been created on a
    /// campus that reached preparation. Once the visit has started, answering is meaningless — which is
    /// the same fact the assignments list states as EXPIRED rather than "Hoàn thành".
    /// </para>
    /// <para>
    /// Exposed separately so the read models and the anonymous email-action path can ask the same
    /// question this method enforces, instead of each carrying its own copy of the window.
    /// </para>
    /// </summary>
    public static bool IsOpenForResponse(string? requestStatus, string? campusStatus)
        => requestStatus != VisitRequestStatuses.Cancelled
           && requestStatus != VisitRequestStatuses.Rejected
           && campusStatus == VisitInstanceStatuses.BeforeVisit;

    /// <summary>
    /// Applies the response to the participant row and files the audit entry. Does NOT save: the caller
    /// owns the transaction, so it decides what commits alongside this.
    /// </summary>
    /// <param name="actorUserId">The authenticated caller. The row must belong to them.</param>
    /// <returns>The mutated participant, with its <c>VisitInstance</c> (and request) loaded.</returns>
    public static async Task<VisitParticipant> ApplyAsync(
        IApplicationDbContext db, ulong actorUserId, ulong participantId,
        bool accept, string? declineReason, System.DateTime now, CancellationToken ct)
    {
        var participant = await db.VisitParticipants
            .Include(p => p.VisitInstance).ThenInclude(v => v.VisitRequest)
            .FirstOrDefaultAsync(p => p.ParticipantId == participantId, ct)
            ?? throw new NotFoundException("VisitParticipant", participantId);

        // Ownership: a user may only respond to their OWN invitation.
        if (participant.UserId != actorUserId)
            throw new ForbiddenException("Bạn chỉ có thể phản hồi lời mời của chính mình.");

        // The host slot is not an invitation; only the 3 non-host invitee roles can respond.
        if (participant.IsHost || participant.ParticipantRole == ParticipantRoles.IcHost)
            throw new ForbiddenException("Lời mời này không thể phản hồi.");

        if (participant.ParticipantRole != ParticipantRoles.IcSupport
            && participant.ParticipantRole != ParticipantRoles.DeptSupport
            && participant.ParticipantRole != ParticipantRoles.Student)
            throw new ForbiddenException("Loại lời mời không hợp lệ.");

        // Must still be pending — already-responded invitations are immutable here.
        if (participant.Status != ParticipantStatuses.Invited && participant.Status != ParticipantStatuses.Assigned)
            throw new ConflictException("Lời mời đã được phản hồi hoặc không còn hiệu lực.");

        var actionName = accept ? "xác nhận tham gia" : "từ chối";
        var requestStatus = participant.VisitInstance?.VisitRequest?.Status;
        var campusStatus = participant.VisitInstance?.Status;

        if (requestStatus == VisitRequestStatuses.Cancelled || requestStatus == VisitRequestStatuses.Rejected
            || campusStatus == VisitInstanceStatuses.Cancelled || campusStatus == VisitInstanceStatuses.Rejected)
            throw new ConflictException($"Không thể {actionName} vì lịch thăm đã bị hủy hoặc từ chối.");

        if (!IsOpenForResponse(requestStatus, campusStatus))
            throw new ConflictException($"Không thể {actionName} vì chuyến thăm đã bắt đầu hoặc kết thúc.");

        if (accept)
        {
            participant.Status = ParticipantStatuses.Accepted;
        }
        else
        {
            // No decline_reason column on visit_participants — record it on note (per schema).
            var reason = declineReason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
                throw new ValidationException("Vui lòng nhập lý do từ chối.");
            participant.Status = ParticipantStatuses.Declined;
            participant.Note = reason;
        }

        participant.RespondedAt = now;
        participant.UpdatedAt = now;
        participant.UpdatedBy = actorUserId;

        // Invite and remove already file the participant's campus; the invitee's own response was the
        // one participant event a campus-filtered audit could not see. It is scoped to a single
        // instance, so it records that instance too.
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            CampusId = participant.VisitInstance?.CampusId,
            Action = accept ? "ACCEPT_VISIT_INVITATION" : "DECLINE_VISIT_INVITATION",
            EntityType = "VisitParticipant",
            EntityId = participant.ParticipantId,
            VisitRequestId = participant.VisitInstance?.VisitRequestId,
            VisitInstanceId = participant.VisitInstanceId,
            CreatedAt = now,
        });

        // An emailed accept/decline link for a row that has just been answered in the app must not
        // still work.
        await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            db, EmailActionTargetTypes.VisitParticipant, participant.ParticipantId,
            "Lời mời này đã được phản hồi.", now, ct);

        return participant;
    }
}
