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

    /// <summary>Minimum gap between two sends of the same invitation.</summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

    /// <summary>An INITIAL_CONFIRMATION link is valid for three days.</summary>
    public const int InitialConfirmationValidityHours = 72;

    /// <summary>A TRANSFER link is valid for one day — it moves an already-working campus.</summary>
    public const int TransferValidityHours = 24;

    /// <summary>Self-service transfer closes this long before the campus starts.</summary>
    public const int TransferLeadHours = 24;

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
    /// The campus is still undecided, so its contact may be replaced outright (plan §3.3). A decided
    /// campus is transfer territory — replacing a contact under a Staff Leader who already committed
    /// resources is exactly what the transfer handshake exists to prevent.
    /// </summary>
    public static void EnsureReplaceWindowOpen(VisitRequest visit, VisitRequestCampus instance)
    {
        EnsureRequestLive(visit);

        if (instance.Status is VisitInstanceStatuses.WaitingContactConfirmation
            or VisitInstanceStatuses.WaitingRequestApproval)
            return;

        throw new ConflictException(
            instance.Status == VisitInstanceStatuses.BeforeVisit
                ? "Cơ sở này đã được duyệt. Đổi đầu mối lúc này phải qua quy trình chuyển giao."
                : "Trạng thái của cơ sở này không cho phép đổi đầu mối vận hành.",
            OperationalContactErrorCodes.ChangeConflict);
    }

    /// <summary>
    /// The campus has a decision and has not started, so its contact may be handed over. Blocked once
    /// the visit is running or finished, and inside the lead time — a handover on the morning of the
    /// visit leaves nobody who has actually been briefed.
    /// </summary>
    public static void EnsureTransferWindowOpen(
        VisitRequest visit, VisitRequestCampus instance, DateTime vietnamNow)
    {
        EnsureRequestLive(visit);

        if (instance.Status != VisitInstanceStatuses.BeforeVisit)
            throw new ConflictException(
                instance.Status is VisitInstanceStatuses.WaitingContactConfirmation
                    or VisitInstanceStatuses.WaitingRequestApproval
                    ? "Cơ sở này chưa được duyệt. Hãy đổi trực tiếp đầu mối vận hành thay vì chuyển giao."
                    : "Trạng thái của cơ sở này không cho phép chuyển giao đầu mối vận hành.",
                OperationalContactErrorCodes.ChangeConflict);

        if (instance.PlannedStartAt.AddHours(-TransferLeadHours) < vietnamNow)
            throw new BusinessRuleException(
                $"Chuyến thăm tại cơ sở này bắt đầu trong vòng {TransferLeadHours} giờ nên không thể tự chuyển giao đầu mối. Vui lòng liên hệ FPTU để được hỗ trợ.",
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
    public static async Task<Domain.Entities.Emails.EmailActionToken> LoadLiveTokenAsync(
        IApplicationDbContext db, IEmailActionTokenService tokens, string rawToken, ulong changeId,
        DateTime vietnamNow, CancellationToken ct)
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

        return token;
    }

    /// <summary>
    /// The account answering must BE the invited address, and it must be usable. Internal accounts are
    /// allowed on purpose (plan §1.7) — an FPTU staff member can be the operational contact of a
    /// campus; what is never allowed is a DIFFERENT account answering somebody else's invitation.
    /// </summary>
    public static void EnsureActorMayTakeContactRole(
        Domain.Entities.Users.User actor, VisitRequestIdentityChange change)
    {
        if (actor.Status != UserStatuses.Active)
            throw new BusinessRuleException(
                "Tài khoản này đang không hoạt động nên không thể nhận vai trò đầu mối vận hành.",
                OperationalContactErrorCodes.AccountInactive);

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
