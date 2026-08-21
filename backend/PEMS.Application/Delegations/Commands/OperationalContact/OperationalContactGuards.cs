using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// The guards every operational-contact command runs before it writes. They exist as one type so the
/// answer to "may this person do this to this campus" is written once: the previous workflow answered
/// it separately in six handlers, and they had already drifted apart on who counts as an owner.
/// </summary>
internal static class OperationalContactGuards
{
    /// <summary>An in-flight invitation may be resent at most this many times.</summary>
    public const int MaxResends = 5;

    /// <summary>
    /// The last step between "an invitation was written" and "the transaction commits": a caller that
    /// has just written a PENDING invitation must not commit unless it also holds the links that answer
    /// it. <c>MintInvitationTokensAsync</c> answers null for "there is nothing to invite" — a row that
    /// is not PENDING, or has no address — which is a legitimate answer for a caller ASKING, and an
    /// impossible one for a caller that wrote the row three lines earlier.
    ///
    /// <para>
    /// So null here is a broken invariant, not a business outcome, and it throws: the exception escapes
    /// before the commit, the transaction rolls back with the invitation inside it, and the caller is
    /// told the operation failed. The alternative — skipping the dispatch and committing anyway —
    /// leaves the campus holding a PENDING change nobody can answer and nobody can replace, which is
    /// the exact state this whole split exists to prevent.
    /// </para>
    /// </summary>
    public static OperationalContactInvitationTokens RequireMintedLinks(
        OperationalContactInvitationTokens? tokens, ulong identityChangeId)
        => tokens ?? throw new InvalidOperationException(
            $"Operational-contact invitation {identityChangeId} was written but produced no usable link; " +
            "the change must not commit without one.");

    /// <summary>Minimum gap between two sends of the same invitation.</summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

    /// <summary>An INITIAL_CONFIRMATION link is valid for three days.</summary>
    public const int InitialConfirmationValidityHours = 72;

    /// <summary>
    /// A TRANSFER link is valid for one day — it moves an already-working campus.
    ///
    /// <para>
    /// This is the INVITATION's validity, and nothing else. It says how long the emailed link stays
    /// answerable; it says nothing about whether answering it is still allowed, which is decided by
    /// the campus's own lifecycle at the moment of the answer (see
    /// <see cref="EnsureTransferWindowOpen"/>). A link can be well inside its day and still be
    /// refused because the visit has since started.
    /// </para>
    /// </summary>
    public const int TransferValidityHours = 24;

    /// <summary>
    /// Any authenticated account. Deliberately NO role bar: the registrant may be VISITOR, STAFF or
    /// STAFF LEADER (plan §1.8), and an operational contact may be an internal account (plan §1.7).
    /// The old workflow demanded RoleCode == VISITOR here, which silently locked staff registrants out
    /// of managing their own request.
    /// </summary>
    public static ulong RequireAuthenticated(
        PerCampusFormV2WriteOptions writeFlag, ICurrentUserService currentUser)
    {
        if (!writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            throw new ForbiddenException();
        return currentUser.UserId.Value;
    }

    /// <summary>
    /// The actor for an accept/decline, which may come from a SESSION or from a proven TOKEN.
    ///
    /// <para>
    /// <paramref name="actingUserId"/> is supplied only by the public (no-login) handlers, and only
    /// after they have validated the single-use token, matched its intended action, and resolved the
    /// invited address to a real account. It is never bound from a request, so it cannot be used to
    /// impersonate: the caller who can set it has already proved more than a session would.
    /// </para>
    /// <para>
    /// The write-flag check stays in force either way — with v2 writes disabled there is no public
    /// door any more than there is an authenticated one.
    /// </para>
    /// </summary>
    public static ulong ResolveActor(
        PerCampusFormV2WriteOptions writeFlag, ICurrentUserService currentUser, ulong? actingUserId)
    {
        if (!writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (actingUserId is { } id)
            return id;
        return RequireAuthenticated(writeFlag, currentUser);
    }

    /// <summary>
    /// Loads the request with all its campuses (tracked) and returns the ONE campus named in the route,
    /// proving it belongs to that request first. Naming a sibling campus — or somebody else's campus —
    /// fails here with a stable code instead of being silently accepted.
    /// </summary>
    public static async Task<(VisitRequest Visit, VisitRequestCampus Instance)> LoadCampusInRequestAsync(
        IApplicationDbContext db, ulong visitRequestId, ulong visitInstanceId, CancellationToken ct)
    {
        var visit = await db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .FirstOrDefaultAsync(v => v.VisitRequestId == visitRequestId, ct)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", visitRequestId);

        var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == visitInstanceId)
            ?? throw new ConflictException(
                "Cơ sở này không thuộc đơn đăng ký đã chọn.",
                CampusScopeErrorCodes.InstanceNotInRequest);

        return (visit, instance);
    }

    /// <summary>
    /// Who may manage the contact of ONE campus: the registrant always, and — when
    /// <paramref name="allowCurrentContact"/> — the confirmed operational contact OF THAT CAMPUS, who
    /// is handing over their own role. Holding a sibling campus grants nothing here.
    /// </summary>
    public static void EnsureMayManageContact(
        VisitRequest visit, VisitRequestCampus instance, ulong actorId, bool allowCurrentContact)
    {
        if (VisitRequestOwnership.IsRegistrant(visit, actorId))
            return;
        if (allowCurrentContact && VisitRequestOwnership.IsOperationalContact(instance, actorId))
            return;

        throw new ForbiddenException(allowCurrentContact
            ? "Chỉ người đăng ký hoặc đầu mối vận hành hiện tại của cơ sở này mới được chuyển giao."
            : "Chỉ người đăng ký mới được thay đổi đầu mối vận hành của cơ sở này.");
    }

    /// <summary>
    /// The campus is still in the pre-start status window, so its contact MAY be replaced outright — but
    /// only when nobody actually holds it yet. This guard is lifecycle-only and does not read
    /// <c>OperationalContactUserId</c>; the caller (<c>ReplaceOperationalContactCommandHandler</c>) adds
    /// the holder check as a separate, explicit refusal, because whether a campus has been DECIDED and
    /// whether it has a CONFIRMED HOLDER are different facts. A campus at WAITING_REQUEST_APPROVAL always
    /// has a holder (the database enforces it), so in practice only WAITING_CONTACT_CONFIRMATION ever
    /// reaches a real replace — this window stays as written so the handler's own refusal, not this one,
    /// is what tells the caller to use transfer instead.
    /// </summary>
    public static void EnsureReplaceWindowOpen(VisitRequest visit, VisitRequestCampus instance)
    {
        EnsureRequestLive(visit);

        if (instance.Status is VisitInstanceStatuses.WaitingContactConfirmation
            or VisitInstanceStatuses.WaitingRequestApproval)
            return;

        throw new ConflictException(
            instance.Status is VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit
                ? "Cơ sở này đã được duyệt. Đổi đầu mối lúc này phải qua quy trình chuyển giao."
                : "Trạng thái của cơ sở này không cho phép đổi đầu mối vận hành.",
            OperationalContactErrorCodes.ChangeConflict);
    }

    /// <summary>
    /// The campus may have its contact's DETAILS corrected — a wider window than replacing or
    /// transferring, because nothing about authority moves, but NOT an unlimited one.
    ///
    /// <para>
    /// The window is the four statuses before the visit starts, and it is a positive whitelist rather
    /// than a list of dead ends: a campus that is already running, finished or closed has a contact
    /// record the visit was actually received against, and rewriting the name or phone on it after the
    /// fact edits history rather than correcting a plan. The two statuses that never had a plan to
    /// correct — cancelled and rejected — are refused by the same rule.
    /// </para>
    /// <para>
    /// No clock is consulted. An approved campus starting in six hours is precisely when a corrected
    /// phone number matters most, so neither the visit-registration lead time (a rule about when a
    /// visit may be SCHEDULED) nor any transfer cutoff has business here — only the persisted status.
    /// </para>
    /// </summary>
    public static void EnsureProfileUpdateAllowed(VisitRequest visit, VisitRequestCampus instance)
    {
        EnsureRequestLive(visit);

        if (instance.Status is
            VisitInstanceStatuses.WaitingContactConfirmation
            or VisitInstanceStatuses.WaitingRequestApproval
            or VisitInstanceStatuses.Assigned
            or VisitInstanceStatuses.BeforeVisit)
            return;

        throw new ConflictException(
            instance.Status is VisitInstanceStatuses.Cancelled or VisitInstanceStatuses.Rejected
                ? "Lịch thăm tại cơ sở này đã kết thúc quy trình nên không thể sửa thông tin đầu mối vận hành."
                : "Chuyến thăm tại cơ sở này đã bắt đầu nên không thể sửa thông tin đầu mối vận hành.",
            OperationalContactErrorCodes.ChangeConflict);
    }

    /// <summary>
    /// A confirmed holder exists and the campus has not started, so its contact may be handed over.
    ///
    /// <para>
    /// Decided by the persisted status ALONE — <c>WAITING_REQUEST_APPROVAL</c>, <c>ASSIGNED</c> or
    /// <c>BEFORE_VISIT</c> and nothing else. WAITING_REQUEST_APPROVAL belongs here even though the
    /// campus has no decision yet: a campus never reaches that status without a confirmed
    /// <c>operational_contact_user_id</c> (the database enforces this — see
    /// <c>trg_visit_campuses_op_contact_guard_bi/bu</c>), so a real holder always exists to hand
    /// something over. The campus's DECISION is a separate question this guard does not ask.
    /// </para>
    /// <para>
    /// There is deliberately no pre-start cutoff: a handover proposed a minute before the visit begins
    /// is allowed while the campus still reads BEFORE_VISIT, and one proposed a week out is refused if
    /// the campus has somehow already been moved to DURING_VISIT. The old 24-hour lead time answered
    /// the question with a clock, which meant the read model and the handler could disagree about a
    /// campus neither of them had actually looked at.
    /// </para>
    /// <para>
    /// Re-run at every point that would MOVE the contact — initiate, accept, resend — because a
    /// transfer that was legal when it was proposed is not thereby legal forever.
    /// </para>
    /// </summary>
    public static void EnsureTransferWindowOpen(VisitRequest visit, VisitRequestCampus instance)
    {
        EnsureRequestLive(visit);

        if (instance.Status is VisitInstanceStatuses.WaitingRequestApproval
            or VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit)
            return;

        throw new ConflictException(
            instance.Status == VisitInstanceStatuses.WaitingContactConfirmation
                ? "Cơ sở này chưa có đầu mối vận hành nên chưa thể chuyển giao. Hãy mời xác nhận trước."
                : "Chuyến thăm tại cơ sở này đã bắt đầu nên không thể chuyển giao đầu mối vận hành.",
            OperationalContactErrorCodes.ChangeConflict);
    }

    private static void EnsureRequestLive(VisitRequest visit)
    {
        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new BusinessRuleException(
                "Đơn đã bị hủy nên không thể thay đổi đầu mối vận hành.",
                OperationalContactErrorCodes.ChangeConflict);
    }

    /// <summary>
    /// Resend budget for ONE invitation: a hard cap plus a cooldown measured from the last link that
    /// was actually minted. Both map to the same rate-limit code, so the caller cannot use the
    /// difference to probe how many invitations an address has received.
    /// </summary>
    public static async Task EnsureResendAllowedAsync(
        IApplicationDbContext db, VisitRequestIdentityChange change, DateTime vietnamNow, CancellationToken ct)
    {
        if (change.ResendCount >= MaxResends)
            throw new BusinessRuleException(
                "Đã vượt quá số lần gửi lại lời mời xác nhận cho cơ sở này.",
                OperationalContactErrorCodes.RateLimited);

        var lastSentAt = await db.EmailActionTokens.AsNoTracking()
            .Where(t => t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange
                        && t.TargetId == change.IdentityChangeId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => (DateTime?)t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (lastSentAt is not null && vietnamNow < lastSentAt.Value.Add(ResendCooldown))
            throw new BusinessRuleException(
                $"Vui lòng đợi {(int)ResendCooldown.TotalSeconds} giây trước khi gửi lại lời mời.",
                OperationalContactErrorCodes.RateLimited);
    }

    /// <summary>
    /// Resolves a raw link to its invitation id by hash. Returns null for anything unknown, so the
    /// caller answers "invalid link" identically for a forged token, a foreign token and a typo — the
    /// endpoint never confirms that a token ever existed.
    /// </summary>
    public static async Task<ulong?> ResolveChangeIdAsync(
        IApplicationDbContext db, IEmailActionTokenService tokens, string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        var hash = tokens.Hash(rawToken.Trim());
        return await db.EmailActionTokens.AsNoTracking()
            .Where(t => t.TokenHash == hash
                        && t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange
                        && (t.ActionContext == EmailActionContexts.VisitContactClaim
                            || t.ActionContext == EmailActionContexts.VisitContactTransfer))
            .Select(t => (ulong?)t.TargetId)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Maps a settled or overdue invitation to its stable error (called on the LOCKED row).</summary>
    public static void EnsurePending(VisitRequestIdentityChange change, DateTime vietnamNow)
    {
        if (change.Status == IdentityChangeStatuses.Superseded)
            throw new ConflictException(
                "Lời mời này đã được thay bằng lời mời mới hơn.",
                OperationalContactErrorCodes.ConfirmationSuperseded);
        if (change.Status != IdentityChangeStatuses.Pending)
            throw new ConflictException(
                "Lời mời không còn hiệu lực (đã được xử lý).",
                OperationalContactErrorCodes.ChangeConflict);
        if (change.ExpiresAt <= vietnamNow)
            throw new ConflictException(
                "Lời mời đã hết hạn. Vui lòng đề nghị người đăng ký gửi lại.",
                OperationalContactErrorCodes.ConfirmationExpired);
    }

    /// <summary>
    /// Loads the tracked token row for THIS raw link and proves it is still live. Single-use is
    /// enforced by <c>used_at</c>: a link that already answered cannot answer again, even if the
    /// invitation somehow returned to PENDING.
    /// </summary>
    /// <param name="requiredIntendedAction">
    /// When given, the token must have been minted FOR this action. An invitation now carries one
    /// link per answer, so a decline link must never be able to accept — otherwise the second link
    /// would be a way around the first, and anything that follows links automatically (a scanner, a
    /// prefetching mail client) could pick the wrong answer. Null keeps the older behaviour for
    /// callers that predate the split.
    /// </param>
    public static async Task<Domain.Entities.Emails.EmailActionToken> LoadLiveTokenAsync(
        IApplicationDbContext db, IEmailActionTokenService tokens, string rawToken, ulong changeId,
        DateTime vietnamNow, CancellationToken ct, string? requiredIntendedAction = null)
    {
        var hash = tokens.Hash(rawToken.Trim());
        var token = await db.EmailActionTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash
                                      && t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange
                                      && t.TargetId == changeId, ct)
            ?? throw new ConflictException(
                "Liên kết không hợp lệ.", OperationalContactErrorCodes.ConfirmationNotFound);

        if (token.UsedAt is not null || token.ResultStatus != EmailActionResultStatuses.Pending)
            throw new ConflictException(
                "Liên kết đã được sử dụng. Vui lòng đề nghị gửi lại lời mời.",
                OperationalContactErrorCodes.ChangeConflict);
        if (token.ExpiresAt <= vietnamNow)
            throw new ConflictException(
                "Liên kết đã hết hạn. Vui lòng đề nghị gửi lại lời mời.",
                OperationalContactErrorCodes.ConfirmationExpired);
        if (requiredIntendedAction is not null
            && !string.Equals(token.IntendedAction, requiredIntendedAction, StringComparison.Ordinal))
            throw new ConflictException(
                "Liên kết này không dùng cho thao tác vừa chọn.",
                OperationalContactErrorCodes.ConfirmationNotFound);

        return token;
    }

    /// <summary>
    /// What a token was minted to do, without consuming or validating it. The public landing page uses
    /// this to render the right single action — the reader followed one of two links, and showing them
    /// both buttons would invite them to press the one their link cannot perform.
    /// </summary>
    public static async Task<string?> IntendedActionOfAsync(
        IApplicationDbContext db, IEmailActionTokenService tokens, string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var hash = tokens.Hash(rawToken.Trim());
        return await db.EmailActionTokens.AsNoTracking()
            .Where(t => t.TokenHash == hash
                        && t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange)
            .Select(t => t.IntendedAction)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// The account answering must BE the invited address, it must be usable, and it must be EXTERNAL.
    ///
    /// <para>
    /// The role bar is the reversal of the old §1.7 allowance: an FPTU account used to be able to hold
    /// a campus's operational contact, and it no longer can — the confirmation exists so somebody
    /// outside FPTU says in their own name that they are bringing the delegation, and an internal
    /// account answering it confirms nothing. What was already never allowed, and still is not, is a
    /// DIFFERENT account answering somebody else's invitation.
    /// </para>
    /// <para>
    /// Checked here rather than only where the address was named, because the two moments are days
    /// apart: an address that belonged to nobody when the invitation was written can belong to a staff
    /// account by the time the link is clicked, and this is the last point before the campus changes
    /// hands.
    /// </para>
    /// </summary>
    public static void EnsureActorMayTakeContactRole(
        Domain.Entities.Users.User actor, VisitRequestIdentityChange change)
    {
        if (actor.Status != UserStatuses.Active)
            throw new BusinessRuleException(
                "Tài khoản này đang không hoạt động nên không thể nhận vai trò đầu mối vận hành.",
                OperationalContactErrorCodes.AccountInactive);

        // Role.RoleCode, from the Include the caller already does. A null navigation would read as
        // "no role", which IsExternalRole answers false to — refusing, which is the safe direction.
        OperationalContactEligibility.EnsureAccountMayHoldContactRole(
            actor.Role?.RoleCode, actor.Status);

        var actorEmail = Services.VisitRequestFingerprintBuilder.NormalizeEmail(actor.Email);
        if (actorEmail != change.NewEmailNormalized)
            throw new ConflictException(
                "Tài khoản đang đăng nhập không trùng với email được mời. Vui lòng đăng nhập đúng tài khoản của email nhận lời mời.",
                OperationalContactErrorCodes.EmailMismatch);
    }

    /// <summary>Validity window for a new invitation of the given kind.</summary>
    public static DateTime ExpiryFor(string changeKind, DateTime vietnamNow)
        => vietnamNow.AddHours(changeKind == IdentityChangeKinds.Transfer
            ? TransferValidityHours
            : InitialConfirmationValidityHours);
}
