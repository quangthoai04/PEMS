using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

    public ExecuteEmailActionCommandHandler(
        IApplicationDbContext db, IDateTimeService clock, IEmailActionTokenService tokens,
        IVisitFormReadService formReadService)
    {
        _db = db;
        _clock = clock;
        _tokens = tokens;
        _formReadService = formReadService;
    }

    // The action is bound to ONE campus instance (token → participant/logistics item → instance), so v2 (incl.
    // mixed) shows THIS instance's per-campus delegation name — never the global field, never a sibling. v1
    // keeps the global value. No global fallback for v2.
    private async Task<string?> ResolveDelegationNameAsync(VisitRequestCampus? instance, CancellationToken ct)
    {
        var visit = instance?.VisitRequest;
        if (visit is null) return null;
        if (visit.FormSchemaVersion < FormSchemaVersions.PerCampus) return visit.DelegationName;
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

        if (token.ActionContext == EmailActionContexts.ParticipationResponse
            && token.TargetType == EmailActionTargetTypes.VisitParticipant)
            return await HandleParticipantAsync(request, token, result, cancellationToken);

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

    // ── Participation accept/decline (UC-27 email path) ──
    private async Task<EmailActionExecuteResult> HandleParticipantAsync(
        ExecuteEmailActionCommand request, Domain.Entities.Emails.EmailActionToken token,
        EmailActionExecuteResult result, CancellationToken cancellationToken)
    {
        var participant = await _db.VisitParticipants
            .FirstOrDefaultAsync(p => p.ParticipantId == token.TargetId, cancellationToken);
        if (participant is null)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = "Không tìm thấy lời mời tương ứng.";
            return result;
        }

        var instance = await _db.VisitRequestCampuses
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

        // Strict target status validation
        if (participant.Status == ParticipantStatuses.Removed)
            return await MarkInvalidAsync(token, request, now, result, "Lời mời này đã bị thu hồi hoặc không còn hiệu lực.", cancellationToken);
        if (participant.Status == ParticipantStatuses.Assigned)
            return await MarkInvalidAsync(token, request, now, result, "Thành phần tham gia đã được phân công trực tiếp, không thể phản hồi qua email.", cancellationToken);
        if (participant.Status == ParticipantStatuses.Accepted || participant.Status == ParticipantStatuses.Declined)
            return await MarkAlreadyRespondedAsync(token, request, now, result, cancellationToken);
        if (participant.Status != ParticipantStatuses.Invited)
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

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);

        participant.Status = isAccept ? ParticipantStatuses.Accepted : ParticipantStatuses.Declined;
        if (!isAccept)
            participant.Note = declineNote; // store the decline reason
        participant.RespondedAt = now;
        participant.UpdatedAt = now;

        ConsumeToken(token, now, request, isAccept ? "Đã chấp nhận lời mời." : "Đã từ chối lời mời.");
        await BurnSiblingsAsync(token, now, cancellationToken);

        if (instance?.CurrentHostUserId is { } hostUserId)
        {
            var verb = isAccept ? "đã chấp nhận" : "đã từ chối";
            _db.Notifications.Add(new Notification
            {
                RecipientUserId = hostUserId,
                NotificationType = "VISIT_PARTICIPANT_RESPONSE",
                Title = "Phản hồi lời mời tham gia",
                Message = $"{result.RecipientName ?? "Người được mời"} {verb} lời mời tham gia đoàn {result.DelegationName ?? string.Empty}.".Trim(),
                RelatedType = "VisitParticipant",
                RelatedId = participant.ParticipantId,
                IsRead = false,
                CreatedAt = now,
            });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = participant.UserId,
            CampusId = instance?.CampusId,
            Action = isAccept ? "PARTICIPANT_RESPONSE_ACCEPT" : "PARTICIPANT_RESPONSE_DECLINE",
            EntityType = "VisitParticipant",
            EntityId = participant.ParticipantId,
            IpAddress = request.Ip,
            UserAgent = Truncate(request.UserAgent, 500),
            CreatedAt = now,
        });

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
        var item = await _db.VisitLogisticsItems
            .FirstOrDefaultAsync(x => x.LogisticsItemId == token.TargetId, cancellationToken);
        if (item is null)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = "Không tìm thấy yêu cầu hậu cần tương ứng.";
            return result;
        }

        var instance = await _db.VisitRequestCampuses
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

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);

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

        ConsumeToken(token, now, request, isAccept ? "Phòng ban đã tiếp nhận yêu cầu." : "Phòng ban đã từ chối yêu cầu.");
        await BurnSiblingsAsync(token, now, cancellationToken);

        if (item.RequestedBy.HasValue)
        {
            var verb = isAccept ? "đã tiếp nhận" : "đã từ chối";
            _db.Notifications.Add(new Notification
            {
                RecipientUserId = item.RequestedBy.Value,
                NotificationType = isAccept ? "VISIT_LOGISTICS_ACCEPTED" : "VISIT_LOGISTICS_REJECTED",
                Title = isAccept ? "Yêu cầu hậu cần được tiếp nhận" : "Yêu cầu hậu cần bị từ chối",
                Message = $"{result.RecipientName ?? "Phòng ban"} {verb} yêu cầu \"{item.Title}\".",
                RelatedType = "LOGISTICS_ITEM",
                RelatedId = item.LogisticsItemId,
                IsRead = false,
                CreatedAt = now,
            });
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
        var item = await _db.VisitLogisticsItems
            .FirstOrDefaultAsync(x => x.LogisticsItemId == token.TargetId, cancellationToken);
        if (item is null)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = "Không tìm thấy nhiệm vụ tương ứng.";
            return result;
        }

        var instance = await _db.VisitRequestCampuses
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

        // Strict validation based on action context
        if (item.Status == LogisticsItemStatus.Accepted)
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

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);

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

        ConsumeToken(token, now, request, isAccept ? "Đã xác nhận yêu cầu/nhiệm vụ." : "Đã từ chối yêu cầu/nhiệm vụ.");
        await BurnSiblingsAsync(token, now, cancellationToken);

        var notifyUserId = item.AssignedBy ?? item.RequestedBy;
        if (notifyUserId.HasValue)
        {
            var verb = isAccept ? "đã nhận/chấp nhận" : "đã từ chối";
            _db.Notifications.Add(new Notification
            {
                RecipientUserId = notifyUserId.Value,
                NotificationType = isAccept ? "VISIT_LOGISTICS_ACCEPTED" : "VISIT_LOGISTICS_DECLINED",
                Title = isAccept ? "Phản hồi yêu cầu hậu cần (Đồng ý)" : "Phản hồi yêu cầu hậu cần (Từ chối)",
                Message = $"{result.RecipientName ?? "Phòng ban/Nhân sự"} {verb} yêu cầu \"{item.Title}\".",
                RelatedType = "LOGISTICS_ITEM",
                RelatedId = item.LogisticsItemId,
                IsRead = false,
                CreatedAt = now,
            });
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

    // ── Host approve/reject of a Department change proposal (LOGISTICS_PROPOSAL_RESPONSE email path) ──
    // Mirrors ConfirmTheChangeProposalCommandHandler so the email and portal paths behave identically.
    private async Task<EmailActionExecuteResult> HandleLogisticsProposalAsync(
        ExecuteEmailActionCommand request, Domain.Entities.Emails.EmailActionToken token,
        EmailActionExecuteResult result, CancellationToken cancellationToken)
    {
        var item = await _db.VisitLogisticsItems
            .FirstOrDefaultAsync(x => x.LogisticsItemId == token.TargetId, cancellationToken);
        if (item is null)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = "Không tìm thấy yêu cầu hậu cần tương ứng.";
            return result;
        }

        var instance = await _db.VisitRequestCampuses
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

        if (instance == null || instance.Status == VisitInstanceStatus.Cancelled || instance.Status == VisitInstanceStatus.Closed)
            return await MarkInvalidAsync(token, request, now, result, "Chuyến tiếp khách này đã bị hủy hoặc đã đóng, liên kết không còn hiệu lực.", cancellationToken);

        // The proposal must still be pending a Host decision.
        if (item.Status != LogisticsItemStatus.ChangeProposed)
        {
            if (item.ProposalResponse != null)
                return await MarkAlreadyRespondedAsync(token, request, now, result, cancellationToken);
            return await MarkInvalidAsync(token, request, now, result, "Đề xuất thay đổi này không còn ở trạng thái chờ phản hồi.", cancellationToken);
        }

        var isApprove = token.IntendedAction == EmailIntendedActions.ApproveProposal;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);

        item.ProposalResponse = isApprove ? "ACCEPTED" : "REJECTED";
        item.ProposalRespondedBy = token.RecipientUserId;
        item.ProposalRespondedAt = now;
        item.UpdatedBy = token.RecipientUserId;
        item.UpdatedAt = now;

        if (isApprove)
        {
            // Apply the proposed time/description onto the originals (quantity stays the PLANNED figure;
            // the final quantity is derived from proposed_quantity when accepted — never overwritten).
            if (item.ProposedUsageStartAt.HasValue) item.UsageStartAt = item.ProposedUsageStartAt.Value;
            if (item.ProposedUsageEndAt.HasValue) item.UsageEndAt = item.ProposedUsageEndAt.Value;
            if (!string.IsNullOrWhiteSpace(item.ProposedDescription)) item.Description = item.ProposedDescription;
            item.Status = LogisticsItemStatus.Accepted;
        }
        else
        {
            item.Status = LogisticsItemStatus.Rejected;
            item.DecisionNote = "Host từ chối đề xuất thay đổi (qua email).";
        }

        ConsumeToken(token, now, request, isApprove ? "Host đã chấp nhận đề xuất thay đổi." : "Host đã từ chối đề xuất thay đổi.");
        await BurnSiblingsAsync(token, now, cancellationToken);

        // Notify whoever raised the proposal (department side).
        var notifyUserId = item.ProposedBy ?? item.AssignedToUserId ?? item.AssignedBy;
        if (notifyUserId.HasValue)
        {
            var verb = isApprove ? "đã chấp nhận" : "đã từ chối";
            _db.Notifications.Add(new Notification
            {
                RecipientUserId = notifyUserId.Value,
                NotificationType = isApprove ? "VISIT_LOGISTICS_PROPOSAL_ACCEPTED" : "VISIT_LOGISTICS_PROPOSAL_REJECTED",
                Title = isApprove ? "Đề xuất thay đổi được chấp nhận" : "Đề xuất thay đổi bị từ chối",
                Message = $"{result.RecipientName ?? "Host"} {verb} đề xuất thay đổi cho yêu cầu \"{item.Title}\".",
                RelatedType = "LOGISTICS_ITEM",
                RelatedId = item.LogisticsItemId,
                IsRead = false,
                CreatedAt = now,
            });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = token.RecipientUserId,
            Action = isApprove ? "LOGISTICS_PROPOSAL_APPROVE" : "LOGISTICS_PROPOSAL_REJECT",
            EntityType = "VisitLogisticsItem",
            EntityId = item.LogisticsItemId,
            IpAddress = request.Ip,
            UserAgent = Truncate(request.UserAgent, 500),
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        result.Status = EmailActionViewStatuses.Success;
        result.Message = isApprove ? "Bạn đã chấp nhận đề xuất thay đổi." : "Bạn đã từ chối đề xuất thay đổi.";
        return result;
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
