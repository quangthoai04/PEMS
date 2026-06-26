using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Notifications;
using PEMS.Domain.Entities.Users;

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

        if (token is null
            || token.ActionContext != EmailActionContexts.ParticipationResponse
            || token.TargetType != EmailActionTargetTypes.VisitParticipant)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = "Liên kết không hợp lệ.";
            return result;
        }

        result.Action = token.IntendedAction;

        var participant = await _db.VisitParticipants
            .FirstOrDefaultAsync(p => p.ParticipantId == token.TargetId, cancellationToken);
        if (participant is null)
        {
            result.Status = EmailActionViewStatuses.Invalid;
            result.Message = "Không tìm thấy lời mời tương ứng.";
            return result;
        }

        // Context for display + the host notification.
        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == participant.VisitInstanceId, cancellationToken);
        result.DelegationName = instance?.VisitRequest?.DelegationName;
        result.RecipientName = await _db.Users
            .Where(u => u.UserId == participant.UserId).Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var now = _clock.UtcNow;

        // ── Idempotency / validity gates (never mutate twice) ──
        if (token.UsedAt != null || token.ResultStatus != EmailActionResultStatuses.Pending)
        {
            result.Status = EmailActionViewStatuses.AlreadyResponded;
            result.Message = "Bạn đã trả lời lời mời này rồi.";
            return result;
        }
        if (token.ExpiresAt < now)
        {
            token.ResultStatus = EmailActionResultStatuses.Expired;
            await _db.SaveChangesAsync(cancellationToken);
            result.Status = EmailActionViewStatuses.Expired;
            result.Message = "Liên kết phản hồi đã hết hạn. Vui lòng liên hệ Host.";
            return result;
        }
        if (participant.Status != ParticipantStatuses.Invited)
        {
            // Already responded through the sibling token or the in-app portal.
            token.UsedAt = now;
            token.ResultStatus = EmailActionResultStatuses.AlreadyResponded;
            token.UsedIp = request.Ip;
            token.UsedUserAgent = Truncate(request.UserAgent, 500);
            await _db.SaveChangesAsync(cancellationToken);
            result.Status = EmailActionViewStatuses.AlreadyResponded;
            result.Message = "Bạn đã trả lời lời mời này rồi.";
            return result;
        }

        var isAccept = token.IntendedAction == EmailIntendedActions.Accept;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);

        // Apply the response.
        participant.Status = isAccept ? ParticipantStatuses.Accepted : ParticipantStatuses.Declined;
        participant.RespondedAt = now;
        participant.UpdatedAt = now;

        // Consume THIS token.
        token.UsedAt = now;
        token.UsedAction = token.IntendedAction;
        token.ResultStatus = EmailActionResultStatuses.Success;
        token.ResultMessage = isAccept ? "Đã chấp nhận lời mời." : "Đã từ chối lời mời.";
        token.UsedIp = request.Ip;
        token.UsedUserAgent = Truncate(request.UserAgent, 500);

        // Burn the sibling token(s) of the same group so the other button can't be used afterwards.
        var siblings = await _db.EmailActionTokens
            .Where(t => t.ActionGroupKey == token.ActionGroupKey
                        && t.EmailActionTokenId != token.EmailActionTokenId
                        && t.ResultStatus == EmailActionResultStatuses.Pending
                        && t.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var s in siblings)
        {
            s.UsedAt = now;
            s.ResultStatus = EmailActionResultStatuses.AlreadyResponded;
            s.ResultMessage = "Lời mời đã được phản hồi qua một liên kết khác.";
        }

        // Notify the host of the response.
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

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
}
