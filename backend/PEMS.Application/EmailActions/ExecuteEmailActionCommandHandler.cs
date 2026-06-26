using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
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

    public ExecuteEmailActionCommandHandler(
        IApplicationDbContext db, IDateTimeService clock, IEmailActionTokenService tokens)
    {
        _db = db;
        _clock = clock;
        _tokens = tokens;
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

        if (token.ActionContext == EmailActionContexts.ParticipationResponse
            && token.TargetType == EmailActionTargetTypes.VisitParticipant)
            return await HandleParticipantAsync(request, token, result, cancellationToken);

        if (token.ActionContext == EmailActionContexts.LogisticsAssigneeResponse
            && token.TargetType == EmailActionTargetTypes.LogisticsItem)
            return await HandleLogisticsAssigneeAsync(request, token, result, cancellationToken);

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
        result.DelegationName = instance?.VisitRequest?.DelegationName;
        result.RecipientName = await _db.Users
            .Where(u => u.UserId == participant.UserId).Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var now = _clock.UtcNow;

        if (token.UsedAt != null || token.ResultStatus != EmailActionResultStatuses.Pending)
            return AlreadyResponded(result);
        if (token.ExpiresAt < now)
            return await ExpireAsync(token, result, cancellationToken);
        if (participant.Status != ParticipantStatuses.Invited)
            return await MarkAlreadyRespondedAsync(token, request, now, result, cancellationToken);

        var isAccept = token.IntendedAction == EmailIntendedActions.Accept;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);

        participant.Status = isAccept ? ParticipantStatuses.Accepted : ParticipantStatuses.Declined;
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

        result.DelegationName = await (
            from c in _db.VisitRequestCampuses
            join v in _db.VisitRequests on c.VisitRequestId equals v.VisitRequestId
            where c.VisitInstanceId == item.VisitInstanceId
            select v.DelegationName).FirstOrDefaultAsync(cancellationToken);
        result.RecipientName = token.RecipientUserId.HasValue
            ? await _db.Users.Where(u => u.UserId == token.RecipientUserId.Value).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;

        var now = _clock.UtcNow;

        if (token.UsedAt != null || token.ResultStatus != EmailActionResultStatuses.Pending)
            return AlreadyResponded(result);
        if (token.ExpiresAt < now)
            return await ExpireAsync(token, result, cancellationToken);
        // The assignment must still be awaiting a response.
        if (item.Status != "ASSIGNED")
            return await MarkAlreadyRespondedAsync(token, request, now, result, cancellationToken);

        var isAccept = token.IntendedAction == EmailIntendedActions.Accept;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);

        if (isAccept)
        {
            item.Status = LogisticsItemStatus.Accepted;          // "ACCEPTED"
            item.AssigneeAcceptedAt = now;
        }
        else
        {
            item.Status = LogisticsItemStatus.Declined;          // "DECLINED" (terminal — no reassign)
        }
        item.UpdatedAt = now;
        item.UpdatedBy = token.RecipientUserId;

        // Mirror onto the latest PENDING attempt for this assignee.
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
            attempt.ResponseSource = "EMAIL";
            attempt.UpdatedAt = now;
        }

        ConsumeToken(token, now, request, isAccept ? "Đã nhận nhiệm vụ." : "Đã từ chối nhiệm vụ.");
        await BurnSiblingsAsync(token, now, cancellationToken);

        // Notify the leader who assigned the task.
        if (item.AssignedBy is { } assignerId)
        {
            var verb = isAccept ? "đã nhận" : "đã từ chối";
            _db.Notifications.Add(new Notification
            {
                RecipientUserId = assignerId,
                NotificationType = isAccept ? "VISIT_LOGISTICS_ACCEPTED" : "VISIT_LOGISTICS_DECLINED",
                Title = isAccept ? "Nhân sự đã nhận nhiệm vụ hậu cần" : "Nhân sự từ chối nhiệm vụ hậu cần",
                Message = $"{result.RecipientName ?? "Nhân sự"} {verb} nhiệm vụ \"{item.Title}\".",
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
        result.Message = isAccept ? "Cảm ơn bạn đã nhận nhiệm vụ hậu cần." : "Bạn đã từ chối nhiệm vụ hậu cần.";
        return result;
    }

    // ── shared helpers ──

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
