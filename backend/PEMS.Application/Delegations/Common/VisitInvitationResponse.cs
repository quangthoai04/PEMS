using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// The ONE implementation of "an invitee answers their invitation" — the locked business mutation
/// shared by Portal (direct invite AND Department-Staff delegated assignment) and the anonymous
/// email-action path.
///
/// <para>
/// There used to be two. <c>RespondVisitParticipantInvitation</c> checked ownership, the participant
/// role, the current status and the visit lifecycle before writing; the department screens went through
/// <c>DepartmentReceptionTasks.AcceptInvitation</c> / <c>DeclineInvitation</c>, which checked none of
/// them — both status guards were commented out, and the participant was loaded by id alone, so posting
/// somebody else's participant id accepted on their behalf. The same business action, INVITED →
/// ACCEPTED, obeyed different rules depending on which screen the button was on. Later, the email path
/// kept a THIRD hand-rolled copy of this state machine that additionally rejected ASSIGNED outright —
/// which broke Department-Staff email responses entirely, because delegation always starts a participant
/// at ASSIGNED.
/// </para>
/// <para>
/// So the transition lives here and every entry point calls it — including Email now. What each caller
/// still owns is what is genuinely channel-specific: the transaction, the account lock, any pre-check
/// specific to that screen (the department calendar's double-booking check), <c>SaveChangesAsync</c>,
/// and — critically — the TOKEN/notification side effects, which differ by channel and therefore live
/// in the caller, not here:
/// <list type="bullet">
/// <item>Portal invalidates every pending email token for the row (a response in the app must retire any
/// outstanding emailed link) — <see cref="EmailActions.EmailTokenInvalidationHelper"/>, called by the
/// caller AFTER this method returns.</item>
/// <item>Email consumes the ONE token it arrived on and burns its siblings in the same action group —
/// <c>ConsumeToken</c>/<c>BurnSiblingsAsync</c> in <c>ExecuteEmailActionCommandHandler</c>, also called
/// by the caller AFTER this method returns.</item>
/// </list>
/// Doing the invalidation inside this method (the old design) meant Email's own broad "invalidate all
/// pending tokens for this participant" call would race its own token-consume step and could clobber the
/// very token being used before it was marked SUCCESS. Splitting the concerns removes that.
/// </para>
/// <para>
/// This method also owns the pessimistic locking for the whole aggregate: it takes the lock hierarchy
/// (VisitRequest → VisitRequestCampus → VisitParticipant, tiers 2-4) before reading anything it makes a
/// decision on, so two concurrent callers — Portal vs Portal, Portal vs Email, Email vs Email, or a
/// response racing a visit cancellation — serialize on the same rows instead of one committing against a
/// stale pre-lock snapshot. What none of them may do any more is write <c>participant.Status</c>, lock
/// tiers 2-4 themselves, or write the business AuditLog row themselves.
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
    /// owns the transaction, so it decides what commits alongside this — and does NOT touch any
    /// <c>EmailActionToken</c>: that is channel-specific and stays with the caller (see class remarks).
    /// </summary>
    /// <param name="actorUserId">
    /// The user the response is attributed to. The row must belong to them. Portal passes the
    /// authenticated caller; Email passes the token's verified <c>RecipientUserId</c> (already checked
    /// to match the participant by the caller before this is invoked).
    /// </param>
    /// <param name="requiredStatus">
    /// The exact participant status this response is only valid from, checked AFTER the row is locked
    /// and reloaded — never on an earlier read. <c>null</c> means Portal's contract: accept either
    /// <see cref="ParticipantStatuses.Invited"/> (direct invite) or <see cref="ParticipantStatuses.Assigned"/>
    /// (delegated assignment), since the Portal UI doesn't need to know which one a given row is. Email
    /// always passes an exact value, because a token minted for one context (e.g. a direct-invite
    /// ACCEPT link) must not succeed against a row that has since moved to the other (e.g. ASSIGNED).
    /// </param>
    /// <param name="note">
    /// Only applied when <paramref name="accept"/> is true: an optional remark the invitee leaves for
    /// the host, trimmed and stored on the same <c>visit_participants.note</c> column
    /// <paramref name="declineReason"/> uses on the decline branch (the two never coexist on one
    /// response, since only one branch of the <c>if (accept)</c> below ever runs). Ignored on decline.
    /// </param>
    /// <returns>The mutated participant, with its <c>VisitInstance</c> (and request) loaded.</returns>
    public static async Task<VisitParticipant> ApplyCoreAsync(
        IApplicationDbContext db, IUserMutationLockService locks, ulong actorUserId, ulong participantId,
        string? requiredStatus, bool accept, string? declineReason, string? note, System.DateTime now, CancellationToken ct)
    {
        // The FKs below are structural (never change on an existing row), so reading them unlocked to
        // discover which tier-2/3 rows to lock is safe — it is the STATUS columns that race, not these.
        var visitInstanceId = await db.VisitParticipants
            .Where(p => p.ParticipantId == participantId)
            .Select(p => (ulong?)p.VisitInstanceId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("VisitParticipant", participantId);

        var visitRequestId = await db.VisitRequestCampuses
            .Where(c => c.VisitInstanceId == visitInstanceId)
            .Select(c => (ulong?)c.VisitRequestId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("VisitRequestCampus", visitInstanceId);

        // Lock hierarchy, tiers 2-4, in this fixed order every writer of this aggregate follows —
        // otherwise a visit cancellation (which locks the same VisitRequest/VisitRequestCampus rows)
        // could commit between this method's read and its write, and this response would go through
        // on a lifecycle snapshot that is no longer true.
        await locks.LockVisitRequestsAsync(new[] { visitRequestId }, ct);
        await locks.LockVisitRequestCampusesAsync(new[] { visitInstanceId }, ct);
        await locks.LockVisitParticipantsAsync(new[] { participantId }, ct);

        // Only now — with tiers 2-4 held — is it safe to read the data this method decides on.
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

        // Must still be pending, and pending in the shape THIS caller expects — evaluated on the
        // just-locked read, never on a snapshot taken before the lock.
        var validNow = requiredStatus is null
            ? participant.Status == ParticipantStatuses.Invited || participant.Status == ParticipantStatuses.Assigned
            : participant.Status == requiredStatus;
        if (!validNow)
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
            var trimmedNote = note?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedNote))
                participant.Note = trimmedNote;
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
        // instance, so it records that instance too. Exactly one audit row per response, regardless of
        // channel — the caller must not also write one.
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

        return participant;
    }
}
