using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Notifications;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.EmailActions;

public sealed class ExecuteEmailActionCommandHandler
    : IRequestHandler<ExecuteEmailActionCommand, EmailActionExecuteResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeService _clock;
    private readonly IEmailActionTokenService _tokens;
    private readonly IVisitFormReadService _formReadService;
    private readonly IUserMutationLockService _locks;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public ExecuteEmailActionCommandHandler(
        IApplicationDbContext db, IDateTimeService clock, IEmailActionTokenService tokens,
        IVisitFormReadService formReadService, IUserMutationLockService locks,
        PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _clock = clock;
        _tokens = tokens;
        _formReadService = formReadService;
        _locks = locks;
        _notificationService = notificationService;
    }

    // The action is bound to ONE campus instance (token → participant/logistics item → instance), so it shows
    // THAT instance's own delegation name — never a sibling's, and never a request-level value, which does
    // not exist. A missing detail is not papered over.
    private async Task<string?> ResolveDelegationNameAsync(VisitRequestCampus? instance, CancellationToken ct)
    {
        var visit = instance?.VisitRequest;
        if (visit is null) return null;
        var content = await _formReadService.ResolveCampusFormContentAsync(
            visit, new[] { instance!.VisitInstanceId }, ct);
        return content[instance.VisitInstanceId].DelegationName;
    }

    public async Task<EmailActionExecuteResult> Handle(
        ExecuteEmailActionCommand request, CancellationToken cancellationToken)
    {
        var result = new EmailActionExecuteResult();
        if (string.IsNullOrWhiteSpace(request.RawToken))
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = "Liên kết không hợp lệ.";
            return result;
        }

        var hash = _tokens.Hash(request.RawToken.Trim());
        var token = await _db.EmailActionTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = "Liên kết không hợp lệ.";
            return result;
        }

        result.Action = token.IntendedAction;
        result.Context = token.ActionContext;

        // v2 identity claim/transfer links are NEVER executable anonymously (plan §4.4): applying one
        // requires a logged-in session whose email matches the invitation. Possession of the email
        // link alone must not grant request ownership — reject here, unused, without touching state.
        if (token.ActionContext == EmailActionContexts.VisitContactClaim
            || token.ActionContext == EmailActionContexts.VisitContactTransfer)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = "Liên kết này yêu cầu đăng nhập. Vui lòng mở trang xác nhận đầu mối liên hệ từ email.";
            return result;
        }

        if (token.ActionContext == EmailActionContexts.ParticipationResponse
            && token.TargetType == EmailActionTargetTypes.VisitParticipant)
            return await HandleParticipantAsync(request, token, result, ParticipantStatuses.Invited, cancellationToken);

        if (token.ActionContext == EmailActionContexts.ParticipationAssignmentResponse
            && token.TargetType == EmailActionTargetTypes.VisitParticipant)
            return await HandleParticipantAsync(request, token, result, ParticipantStatuses.Assigned, cancellationToken);

        if (token.ActionContext == EmailActionContexts.LogisticsRequestResponse
            && token.TargetType == EmailActionTargetTypes.LogisticsItem)
            return await HandleLogisticsRequestAsync(request, token, result, cancellationToken);

        if (token.ActionContext == EmailActionContexts.LogisticsAssigneeResponse
            && token.TargetType == EmailActionTargetTypes.LogisticsItem)
            return await HandleLogisticsAssigneeAsync(request, token, result, cancellationToken);

        if (token.ActionContext == EmailActionContexts.LogisticsProposalResponse
            && token.TargetType == EmailActionTargetTypes.LogisticsItem)
            return await HandleLogisticsProposalAsync(request, token, result, cancellationToken);

        result.Status = EmailActionViewStatuses.Invalid;
        result.Message = "Liên kết không hợp lệ.";
        return result;
    }

    // ── Participation accept/decline (UC-27 email path + Department-Staff assignment email path) ──
    // requiredStatus is the ONE participant status this specific token's context is valid from —
    // Invited for a direct invitation, Assigned for a delegated Staff assignment (see
    // EmailActionContexts.ParticipationAssignmentResponse). The checks below using it are a fast,
    // pre-transaction UX path only (so an obviously-dead token doesn't need a transaction at all); the
    // AUTHORITATIVE check is inside VisitInvitationResponse.ApplyCoreAsync, evaluated after the row is
    // locked — a race between this pre-check and the lock is caught there, not trusted here.
    private async Task<EmailActionExecuteResult> HandleParticipantAsync(
        ExecuteEmailActionCommand request, Domain.Entities.Emails.EmailActionToken token,
        EmailActionExecuteResult result, string requiredStatus, CancellationToken cancellationToken)
    {
        // AsNoTracking: this is a pre-transaction fast-path/display read only. Loading it tracked would
        // make EF's identity map hand ApplyCoreAsync's later, lock-protected re-query this SAME stale
        // instance instead of re-hitting the database — silently defeating the whole point of locking.
        var participant = await _db.VisitParticipants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ParticipantId == token.TargetId, cancellationToken);
        if (participant is null)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = "Không tìm thấy lời mời tương ứng.";
            return result;
        }

        var instance = await _db.VisitRequestCampuses.AsNoTracking()
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == participant.VisitInstanceId, cancellationToken);

        result.DelegationName = await ResolveDelegationNameAsync(instance, cancellationToken);
        result.RecipientName = await _db.Users
            .Where(u => u.UserId == participant.UserId).Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var now = _clock.VietnamNow;

        if (token.ResultStatus == EmailActionResultStatuses.Invalid)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = token.ResultMessage ?? "Lời mời này đã bị thu hồi hoặc không còn hiệu lực.";
            return result;
        }
        if (token.ResultStatus == EmailActionResultStatuses.Expired || token.ExpiresAt < now)
            return await ExpireAsync(token, result, cancellationToken);
        if (token.ResultStatus == EmailActionResultStatuses.AlreadyResponded || token.UsedAt != null || token.ResultStatus == EmailActionResultStatuses.Success)
            return AlreadyResponded(result);

        // Parent validation
        if (instance == null || instance.Status == VisitInstanceStatus.Cancelled || instance.Status == VisitInstanceStatus.Closed)
            return await MarkInvalidAsync(token, request, now, result, "Đoàn khách đã bị hủy hoặc đã đóng, không thể thao tác.", cancellationToken);

        // The same window the in-app Accept/Decline enforces (see VisitInvitationResponse): an emailed
        // link is a third door onto one business action, and it must not be the one that stays open
        // after the visit has already started.
        if (!PEMS.Application.Delegations.Common.VisitInvitationResponse.IsOpenForResponse(
                instance.VisitRequest?.Status, instance.Status))
            return await MarkInvalidAsync(token, request, now, result,
                "Chuyến thăm đã bắt đầu hoặc kết thúc, không thể phản hồi lời mời.", cancellationToken);

        // Fast-path target status validation (see method remarks — re-verified under lock below).
        if (participant.Status == ParticipantStatuses.Removed)
            return await MarkInvalidAsync(token, request, now, result, "Lời mời này đã bị thu hồi hoặc không còn hiệu lực.", cancellationToken);
        if (participant.Status == ParticipantStatuses.Accepted || participant.Status == ParticipantStatuses.Declined)
            return await MarkAlreadyRespondedAsync(token, request, now, result, cancellationToken);
        if (participant.Status != requiredStatus)
            return await MarkInvalidAsync(token, request, now, result, "Trạng thái lời mời không hợp lệ.", cancellationToken);

        var isAccept = token.IntendedAction == EmailIntendedActions.Accept;

        // A decline must carry a reason
        string? declineNote = null;
        if (!isAccept)
        {
            var reasonError = ValidateDeclineReason(request.DeclineReason);
            if (reasonError != null)
            {
                result.Status = EmailActionViewStatuses.ReasonRequired;
                result.Message = reasonError;
                result.SubmittedReason = request.DeclineReason;
                return result; // token untouched, no DB write
            }
            declineNote = request.DeclineReason!.Trim();
        }

        await using var transaction = await _db.BeginSerializedTransactionAsync(cancellationToken);

        PEMS.Domain.Entities.Delegations.VisitParticipant respondedParticipant;
        try
        {
            // Locks the tier 2-4 hierarchy (VisitRequest → VisitRequestCampus → VisitParticipant),
            // re-validates ownership/role/lifecycle/status on that just-locked read, mutates and writes
            // the ONE audit row — the exact same transition Portal uses. The token itself is untouched
            // by this call; token-side effects are this method's own responsibility (below), not
            // ApplyCoreAsync's, because Email's token bookkeeping differs from Portal's (see
            // VisitInvitationResponse's class remarks).
            respondedParticipant = await PEMS.Application.Delegations.Common.VisitInvitationResponse.ApplyCoreAsync(
                _db, _locks, actorUserId: participant.UserId, participant.ParticipantId, requiredStatus,
                isAccept, declineNote, now, cancellationToken);
        }
        catch (ConflictException)
        {
            // The locked, fresh read disagreed with the fast pre-check above — almost always because a
            // concurrent response (Portal or another Email click) landed in between. Report it as
            // already-responded rather than letting an unhandled exception surface.
            await transaction.RollbackAsync(cancellationToken);
            return await MarkAlreadyRespondedAsync(token, request, now, result, cancellationToken);
        }
        catch (Exception ex) when (ex is ForbiddenException or NotFoundException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await MarkInvalidAsync(token, request, now, result, "Liên kết không còn hợp lệ.", cancellationToken);
        }

        // Tier 5 — re-lock and re-read the token group INSIDE the transaction, right before consuming
        // it. Never trust the token object fetched before the transaction opened: a concurrent Portal
        // response's invalidation, or a second Email click on a sibling token, could have committed
        // against these exact rows between that earlier read and now.
        await _locks.LockEmailActionTokenGroupAsync(token.ActionGroupKey, cancellationToken);
        // `token` is already tracked (loaded at the top of Handle, before any lock), so re-querying by
        // its id through the normal DbSet would just hand back that SAME stale tracked instance via
        // EF's identity map, not fresh data. IApplicationDbContext deliberately doesn't expose
        // DbContext.Entry/ReloadAsync, so an AsNoTracking projection is the independent fresh read —
        // once that confirms the row is still genuinely pending, mutating the tracked `token` below is
        // safe (nothing about it was read from a stale snapshot; only written to one now known-current).
        var freshTokenState = await _db.EmailActionTokens.AsNoTracking()
            .Where(t => t.EmailActionTokenId == token.EmailActionTokenId)
            .Select(t => new { t.ResultStatus, t.UsedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (freshTokenState is null || freshTokenState.ResultStatus != EmailActionResultStatuses.Pending || freshTokenState.UsedAt != null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AlreadyResponded(result);
        }

        ConsumeToken(token, now, request, isAccept ? "Đã chấp nhận lời mời." : "Đã từ chối lời mời.");
        await BurnSiblingsAsync(token, now, cancellationToken);

        if (instance?.CurrentHostUserId is { } hostUserId)
        {
            var verb = isAccept ? "đã chấp nhận" : "đã từ chối";
            await _notificationService.CreateAsync(
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: hostUserId,
                    Title: "Phản hồi lời mời tham gia",
                    Message: $"{result.RecipientName ?? "Người được mời"} {verb} lời mời tham gia đoàn {result.DelegationName ?? string.Empty}.".Trim(),
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.ParticipationResponded,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitParticipant,
                    RelatedId: respondedParticipant.ParticipantId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Invitation,
                    VisitInstanceId: respondedParticipant.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit/process/{respondedParticipant.VisitInstanceId}",
                    MetadataJson: isAccept
                        ? PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                            PEMS.Application.Notifications.Common.NotificationEventKeys.ParticipationAccepted,
                            new { delegationName = result.DelegationName, participantName = result.RecipientName })
                        : PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                            PEMS.Application.Notifications.Common.NotificationEventKeys.ParticipationDeclined,
                            new { delegationName = result.DelegationName, participantName = result.RecipientName, reason = declineNote })),
                cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        result.Status = EmailActionViewStatuses.Success;
        result.Message = isAccept
            ? "Cảm ơn bạn đã chấp nhận lời mời tham gia."
            : "Bạn đã từ chối lời mời tham gia.";
        return result;
    }

    // ── Logistics request accept/decline (Sent to Department) ──
    private async Task<EmailActionExecuteResult> HandleLogisticsRequestAsync(
        ExecuteEmailActionCommand request, Domain.Entities.Emails.EmailActionToken token,
        EmailActionExecuteResult result, CancellationToken cancellationToken)
    {
        // AsNoTracking — pre-transaction fast-path/display read only (see the lock hierarchy + fresh
        // reload inside the transaction below).
        var item = await _db.VisitLogisticsItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LogisticsItemId == token.TargetId, cancellationToken);
        if (item is null)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = "Không tìm thấy yêu cầu hậu cần tương ứng.";
            return result;
        }

        var instance = await _db.VisitRequestCampuses.AsNoTracking()
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == item.VisitInstanceId, cancellationToken);

        result.DelegationName = await ResolveDelegationNameAsync(instance, cancellationToken);
        result.RecipientName = token.RecipientUserId.HasValue
            ? await _db.Users.Where(u => u.UserId == token.RecipientUserId.Value).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;

        var now = _clock.VietnamNow;

        if (token.ResultStatus == EmailActionResultStatuses.Invalid)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = token.ResultMessage ?? "Yêu cầu này không còn hiệu lực.";
            return result;
        }
        if (token.ResultStatus == EmailActionResultStatuses.Expired || token.ExpiresAt < now)
            return await ExpireAsync(token, result, cancellationToken);
        if (token.ResultStatus == EmailActionResultStatuses.AlreadyResponded || token.UsedAt != null || token.ResultStatus == EmailActionResultStatuses.Success)
            return AlreadyResponded(result);

        if (instance == null || instance.Status == VisitInstanceStatus.Cancelled || instance.Status == VisitInstanceStatus.Closed)
            return await MarkInvalidAsync(token, request, now, result, "Chuyến tiếp khách này đã bị hủy hoặc đã đóng, liên kết không còn hiệu lực.", cancellationToken);

        if (item.Status != LogisticsItemStatus.Requested)
        {
            if (item.Status == LogisticsItemStatus.Assigned || item.Status == LogisticsItemStatus.Accepted || item.Status == LogisticsItemStatus.Done)
                return await MarkAlreadyRespondedAsync(token, request, now, result, cancellationToken);
            return await MarkInvalidAsync(token, request, now, result, "Yêu cầu hậu cần này không còn ở trạng thái chờ phòng ban phản hồi.", cancellationToken);
        }

        var isAccept = token.IntendedAction == EmailIntendedActions.Accept;

        // A DECLINE must carry a reason (saved to decision_note) — same gate as the participant flow.
        // A missing/invalid reason returns REASON_REQUIRED and leaves the token untouched so the public
        // page re-renders the form (no second mutation).
        string? declineNote = null;
        if (!isAccept)
        {
            var reasonError = ValidateLogisticsDeclineReason(request.DeclineReason);
            if (reasonError != null)
            {
                result.Status = EmailActionViewStatuses.ReasonRequired;
                result.Message = reasonError;
                result.SubmittedReason = request.DeclineReason;
                return result; // token untouched, no DB write
            }
            declineNote = request.DeclineReason!.Trim();
        }

        await using var transaction = await _db.BeginSerializedTransactionAsync(cancellationToken);

        // Lock hierarchy (VisitRequest → VisitRequestCampus → VisitLogisticsItem), then re-read the
        // item tracked and fresh — the earlier read was AsNoTracking, and a concurrent Portal response
        // or cancellation could have moved its status in between.
        await _locks.LockVisitRequestsAsync(new[] { instance!.VisitRequestId }, cancellationToken);
        await _locks.LockVisitRequestCampusesAsync(new[] { instance.VisitInstanceId }, cancellationToken);
        await _locks.LockVisitLogisticsItemsAsync(new[] { item.LogisticsItemId }, cancellationToken);
        item = await _db.VisitLogisticsItems
            .FirstOrDefaultAsync(x => x.LogisticsItemId == token.TargetId, cancellationToken)
            ?? throw new NotFoundException("VisitLogisticsItem", token.TargetId);
        if (item.Status != LogisticsItemStatus.Requested)
        {
            await transaction.RollbackAsync(cancellationToken);
            return item.Status is LogisticsItemStatus.Assigned or LogisticsItemStatus.Accepted or LogisticsItemStatus.Done
                ? await MarkAlreadyRespondedAsync(token, request, now, result, cancellationToken)
                : await MarkInvalidAsync(token, request, now, result, "Yêu cầu hậu cần này không còn ở trạng thái chờ phòng ban phản hồi.", cancellationToken);
        }

        if (isAccept)
        {
            item.Status = LogisticsItemStatus.Accepted;
        }
        else
        {
            item.Status = LogisticsItemStatus.Rejected;
            item.DecisionNote = declineNote;   // lý do từ chối (decision_note) — validated above
        }
        item.UpdatedAt = now;
        item.UpdatedBy = token.RecipientUserId;

        // Tier 5 — re-lock and independently re-verify the token group is still pending, right before
        // consuming it (see HandleParticipantAsync's remarks for why this can't just reload `token`
        // via EF's identity map).
        await _locks.LockEmailActionTokenGroupAsync(token.ActionGroupKey, cancellationToken);
        var freshTokenState = await _db.EmailActionTokens.AsNoTracking()
            .Where(t => t.EmailActionTokenId == token.EmailActionTokenId)
            .Select(t => new { t.ResultStatus, t.UsedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (freshTokenState is null || freshTokenState.ResultStatus != EmailActionResultStatuses.Pending || freshTokenState.UsedAt != null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AlreadyResponded(result);
        }

        ConsumeToken(token, now, request, isAccept ? "Phòng ban đã tiếp nhận yêu cầu." : "Phòng ban đã từ chối yêu cầu.");
        await BurnSiblingsAsync(token, now, cancellationToken);

        if (item.RequestedBy.HasValue)
        {
            var verb = isAccept ? "đã tiếp nhận" : "đã từ chối";
            await _notificationService.CreateAsync(
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: item.RequestedBy.Value,
                    Title: isAccept ? "Yêu cầu hậu cần được tiếp nhận" : "Yêu cầu hậu cần bị từ chối",
                    Message: $"{result.RecipientName ?? "Phòng ban"} {verb} yêu cầu \"{item.Title}\".",
                    NotificationType: isAccept ? "VISIT_LOGISTICS_ACCEPTED" : "VISIT_LOGISTICS_REJECTED",
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                    RelatedId: item.LogisticsItemId,
                    ActorUserId: token.RecipientUserId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                    VisitInstanceId: item.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                    ActionUrl: $"/dashboard/visit/process/{item.VisitInstanceId}"),
                cancellationToken);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = token.RecipientUserId,
            Action = isAccept ? "LOGISTICS_REQUEST_ACCEPT" : "LOGISTICS_REQUEST_REJECT",
            EntityType = "VisitLogisticsItem",
            EntityId = item.LogisticsItemId,
            IpAddress = request.Ip,
            UserAgent = Truncate(request.UserAgent, 500),
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        result.Status = EmailActionViewStatuses.Success;
        result.Message = isAccept ? "Phòng ban đã tiếp nhận yêu cầu thành công." : "Phòng ban đã từ chối yêu cầu.";
        return result;
    }

    // ── Logistics assignee accept/decline (Part B2 email path) ──
    private async Task<EmailActionExecuteResult> HandleLogisticsAssigneeAsync(
        ExecuteEmailActionCommand request, Domain.Entities.Emails.EmailActionToken token,
        EmailActionExecuteResult result, CancellationToken cancellationToken)
    {
        // AsNoTracking — pre-transaction fast-path/display read only, same reasoning as
        // HandleLogisticsRequestAsync.
        var item = await _db.VisitLogisticsItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LogisticsItemId == token.TargetId, cancellationToken);
        if (item is null)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = "Không tìm thấy nhiệm vụ tương ứng.";
            return result;
        }

        var instance = await _db.VisitRequestCampuses.AsNoTracking()
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == item.VisitInstanceId, cancellationToken);

        result.DelegationName = await ResolveDelegationNameAsync(instance, cancellationToken);
        result.RecipientName = token.RecipientUserId.HasValue
            ? await _db.Users.Where(u => u.UserId == token.RecipientUserId.Value).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;

        var now = _clock.VietnamNow;

        if (token.ResultStatus == EmailActionResultStatuses.Invalid)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = token.ResultMessage ?? "Liên kết không còn hiệu lực.";
            return result;
        }
        if (token.ResultStatus == EmailActionResultStatuses.Expired || token.ExpiresAt < now)
            return await ExpireAsync(token, result, cancellationToken);
        if (token.ResultStatus == EmailActionResultStatuses.AlreadyResponded || token.UsedAt != null || token.ResultStatus == EmailActionResultStatuses.Success)
            return AlreadyResponded(result);

        // Parent validation
        if (instance == null || instance.Status == VisitInstanceStatus.Cancelled || instance.Status == VisitInstanceStatus.Closed)
            return await MarkInvalidAsync(token, request, now, result, "Chuyến tiếp khách này đã bị hủy hoặc đã đóng, liên kết không còn hiệu lực.", cancellationToken);

        // Strict validation based on action context. Declined is a terminal STAFF decision on this
        // same item (spec §1.2/§6) — like Accepted, a stale link arriving afterward is "you already
        // responded", not "invalid", matching this method's own post-lock re-check below and
        // HandleParticipantAsync's fast path.
        if (item.Status == LogisticsItemStatus.Accepted || item.Status == LogisticsItemStatus.Declined)
            return await MarkAlreadyRespondedAsync(token, request, now, result, cancellationToken);
        if (item.Status == LogisticsItemStatus.Cancelled || item.Status == LogisticsItemStatus.Rejected || item.Status == LogisticsItemStatus.Done)
            return await MarkInvalidAsync(token, request, now, result, "Nhiệm vụ hậu cần đã bị hủy, từ chối hoặc đã hoàn thành.", cancellationToken);

        // Enforce recipient matching for Assigned items
        if (item.Status != LogisticsItemStatus.Assigned)
        {
            return await MarkInvalidAsync(token, request, now, result, "Trạng thái nhiệm vụ hậu cần không hợp lệ để phản hồi.", cancellationToken);
        }
        if (item.AssignedToUserId != token.RecipientUserId)
        {
            return await MarkInvalidAsync(token, request, now, result, "Bạn không còn là người phụ trách yêu cầu này.", cancellationToken);
        }

        var isAccept = token.IntendedAction == EmailIntendedActions.Accept;

        await using var transaction = await _db.BeginSerializedTransactionAsync(cancellationToken);

        // Lock hierarchy, then re-read the item tracked and fresh — same reasoning as
        // HandleLogisticsRequestAsync.
        await _locks.LockVisitRequestsAsync(new[] { instance!.VisitRequestId }, cancellationToken);
        await _locks.LockVisitRequestCampusesAsync(new[] { instance.VisitInstanceId }, cancellationToken);
        await _locks.LockVisitLogisticsItemsAsync(new[] { item.LogisticsItemId }, cancellationToken);
        item = await _db.VisitLogisticsItems
            .FirstOrDefaultAsync(x => x.LogisticsItemId == token.TargetId, cancellationToken)
            ?? throw new NotFoundException("VisitLogisticsItem", token.TargetId);
        if (item.Status == LogisticsItemStatus.Accepted || item.Status == LogisticsItemStatus.Declined)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await MarkAlreadyRespondedAsync(token, request, now, result, cancellationToken);
        }
        if (item.Status != LogisticsItemStatus.Assigned || item.AssignedToUserId != token.RecipientUserId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await MarkInvalidAsync(token, request, now, result, "Trạng thái nhiệm vụ hậu cần không hợp lệ để phản hồi.", cancellationToken);
        }

        if (isAccept)
        {
            item.Status = LogisticsItemStatus.Accepted;
            item.AssigneeAcceptedAt = now;
        }
        else
        {
            item.Status = LogisticsItemStatus.Declined; // terminal
        }
        item.UpdatedAt = now;
        item.UpdatedBy = token.RecipientUserId;

        var attempt = await _db.VisitLogisticsAssignmentAttempts
            .Where(a => a.LogisticsItemId == item.LogisticsItemId
                        && a.AssigneeUserId == token.RecipientUserId
                        && a.Status == "PENDING")
            .OrderByDescending(a => a.AssignedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (attempt != null)
        {
            attempt.Status = isAccept ? "ACCEPTED" : "DECLINED";
            attempt.RespondedAt = now;
            attempt.ResponseSource = "EMAIL_TOKEN";
            attempt.UpdatedAt = now;
        }

        // Tier 5 — same independent re-verify as HandleLogisticsRequestAsync/HandleParticipantAsync.
        await _locks.LockEmailActionTokenGroupAsync(token.ActionGroupKey, cancellationToken);
        var freshTokenState = await _db.EmailActionTokens.AsNoTracking()
            .Where(t => t.EmailActionTokenId == token.EmailActionTokenId)
            .Select(t => new { t.ResultStatus, t.UsedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (freshTokenState is null || freshTokenState.ResultStatus != EmailActionResultStatuses.Pending || freshTokenState.UsedAt != null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AlreadyResponded(result);
        }

        ConsumeToken(token, now, request, isAccept ? "Đã xác nhận yêu cầu/nhiệm vụ." : "Đã từ chối yêu cầu/nhiệm vụ.");
        await BurnSiblingsAsync(token, now, cancellationToken);

        var notifyUserId = item.AssignedBy ?? item.RequestedBy;
        if (notifyUserId.HasValue)
        {
            var verb = isAccept ? "đã nhận/chấp nhận" : "đã từ chối";
            await _notificationService.CreateAsync(
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: notifyUserId.Value,
                    Title: isAccept ? "Phản hồi yêu cầu hậu cần (Đồng ý)" : "Phản hồi yêu cầu hậu cần (Từ chối)",
                    Message: $"{result.RecipientName ?? "Phòng ban/Nhân sự"} {verb} yêu cầu \"{item.Title}\".",
                    NotificationType: isAccept ? "VISIT_LOGISTICS_ACCEPTED" : "VISIT_LOGISTICS_DECLINED",
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                    RelatedId: item.LogisticsItemId,
                    ActorUserId: token.RecipientUserId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                    VisitInstanceId: item.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                    ActionUrl: $"/dashboard/visit/process/{item.VisitInstanceId}"),
                cancellationToken);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = token.RecipientUserId,
            Action = isAccept ? "LOGISTICS_ASSIGNEE_ACCEPT" : "LOGISTICS_ASSIGNEE_DECLINE",
            EntityType = "VisitLogisticsItem",
            EntityId = item.LogisticsItemId,
            IpAddress = request.Ip,
            UserAgent = Truncate(request.UserAgent, 500),
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        result.Status = EmailActionViewStatuses.Success;
        result.Message = isAccept ? "Cảm ơn bạn đã xác nhận yêu cầu hậu cần." : "Bạn đã từ chối yêu cầu hậu cần.";
        return result;
    }

    // ── Legacy proposal tokens only (LOGISTICS_PROPOSAL_RESPONSE email path) ──
    // Proposal decisions are Portal-only (spec BUG-07): ProposeRequestChangeCommand no longer mints
    // tokens in this context, so only a token minted before that fix can still reach this branch. It
    // must never mutate — the whole point of the business rule is that a proposal can only be decided
    // after the Host logs in, and a legacy public token bypassing that would be exactly the gap the
    // rule closes. This always resolves to Invalid (via MarkInvalidAsync) or AlreadyResponded, and
    // points the recipient at the Portal instead.
    private async Task<EmailActionExecuteResult> HandleLogisticsProposalAsync(
        ExecuteEmailActionCommand request, Domain.Entities.Emails.EmailActionToken token,
        EmailActionExecuteResult result, CancellationToken cancellationToken)
    {
        var now = _clock.VietnamNow;

        if (token.ResultStatus == EmailActionResultStatuses.AlreadyResponded || token.UsedAt != null || token.ResultStatus == EmailActionResultStatuses.Success)
            return AlreadyResponded(result);

        return await MarkInvalidAsync(
            token, request, now, result,
            "Đề xuất thay đổi hậu cần chỉ có thể được xử lý sau khi đăng nhập vào hệ thống. Vui lòng đăng nhập và mở mục Hậu cần để Chấp nhận / Từ chối đề xuất.",
            cancellationToken);
    }

    // ── shared helpers ──

    /// <summary>Returns a Vietnamese error message when the decline reason is missing/too short/too
    /// long (validated on the trimmed value), or null when it is acceptable.</summary>
    private static string? ValidateDeclineReason(string? reason)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return "Vui lòng nhập lý do từ chối.";
        if (trimmed.Length < 5) return "Lý do từ chối phải có ít nhất 5 ký tự.";
        if (trimmed.Length > 1000) return "Lý do từ chối không được vượt quá 1000 ký tự.";
        return null;
    }

    /// <summary>Same 5–1000 rule as the participant decline, with a logistics-specific empty message.</summary>
    private static string? ValidateLogisticsDeclineReason(string? reason)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return "Vui lòng nhập lý do từ chối yêu cầu logistics.";
        if (trimmed.Length < 5) return "Lý do từ chối phải có ít nhất 5 ký tự.";
        if (trimmed.Length > 1000) return "Lý do từ chối không được vượt quá 1000 ký tự.";
        return null;
    }

    private static EmailActionExecuteResult AlreadyResponded(EmailActionExecuteResult result)
    {
        result.Status = EmailActionViewStatuses.AlreadyResponded;
        result.Message = "Bạn đã phản hồi liên kết này rồi.";
        return result;
    }

    private async Task<EmailActionExecuteResult> ExpireAsync(
        Domain.Entities.Emails.EmailActionToken token, EmailActionExecuteResult result, CancellationToken ct)
    {
        token.ResultStatus = EmailActionResultStatuses.Expired;
        await _db.SaveChangesAsync(ct);
        result.Status = EmailActionViewStatuses.Expired;
        result.Message = "Liên kết phản hồi đã hết hạn. Vui lòng liên hệ người gửi.";
        return result;
    }

    private async Task<EmailActionExecuteResult> MarkInvalidAsync(
        Domain.Entities.Emails.EmailActionToken token, ExecuteEmailActionCommand request, System.DateTime now,
        EmailActionExecuteResult result, string message, CancellationToken ct)
    {
        token.UsedAt = now;
        token.ResultStatus = EmailActionResultStatuses.Invalid;
        token.ResultMessage = message;
        token.UsedIp = request.Ip;
        token.UsedUserAgent = Truncate(request.UserAgent, 500);
        await _db.SaveChangesAsync(ct);
        result.Status = EmailActionViewStatuses.Invalid;
        result.Message = message;
        return result;
    }

    private async Task<EmailActionExecuteResult> MarkAlreadyRespondedAsync(
        Domain.Entities.Emails.EmailActionToken token, ExecuteEmailActionCommand request, System.DateTime now,
        EmailActionExecuteResult result, CancellationToken ct)
    {
        token.UsedAt = now;
        token.ResultStatus = EmailActionResultStatuses.AlreadyResponded;
        token.UsedIp = request.Ip;
        token.UsedUserAgent = Truncate(request.UserAgent, 500);
        await _db.SaveChangesAsync(ct);
        result.Status = EmailActionViewStatuses.AlreadyResponded;
        result.Message = "Yêu cầu này đã được phản hồi trước đó.";
        return result;
    }

    private static void ConsumeToken(
        Domain.Entities.Emails.EmailActionToken token, System.DateTime now, ExecuteEmailActionCommand request, string message)
    {
        token.UsedAt = now;
        token.UsedAction = token.IntendedAction;
        token.ResultStatus = EmailActionResultStatuses.Success;
        token.ResultMessage = message;
        token.UsedIp = request.Ip;
        token.UsedUserAgent = Truncate(request.UserAgent, 500);
    }

    private async Task BurnSiblingsAsync(
        Domain.Entities.Emails.EmailActionToken token, System.DateTime now, CancellationToken ct)
    {
        var siblings = await _db.EmailActionTokens
            .Where(t => t.ActionGroupKey == token.ActionGroupKey
                        && t.EmailActionTokenId != token.EmailActionTokenId
                        && t.ResultStatus == EmailActionResultStatuses.Pending
                        && t.UsedAt == null)
            .ToListAsync(ct);
        foreach (var s in siblings)
        {
            s.UsedAt = now;
            s.ResultStatus = EmailActionResultStatuses.AlreadyResponded;
            s.ResultMessage = "Yêu cầu đã được phản hồi qua một liên kết khác.";
        }
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
}
