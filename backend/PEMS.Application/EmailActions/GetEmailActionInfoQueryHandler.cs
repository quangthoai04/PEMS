using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

namespace PEMS.Application.EmailActions;

public sealed class GetEmailActionInfoQueryHandler
    : IRequestHandler<GetEmailActionInfoQuery, EmailActionInfoResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeService _clock;
    private readonly IEmailActionTokenService _tokens;

    public GetEmailActionInfoQueryHandler(
        IApplicationDbContext db, IDateTimeService clock, IEmailActionTokenService tokens)
    {
        _db = db;
        _clock = clock;
        _tokens = tokens;
    }

    public async Task<EmailActionInfoResult> Handle(
        GetEmailActionInfoQuery request, CancellationToken cancellationToken)
    {
        var invalid = new EmailActionInfoResult { Status = EmailActionViewStatuses.Invalid };
        if (string.IsNullOrWhiteSpace(request.RawToken))
            return invalid;

        var hash = _tokens.Hash(request.RawToken.Trim());
        var token = await _db.EmailActionTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is null)
            return invalid;

        var result = new EmailActionInfoResult { Action = token.IntendedAction };

        if (token.ActionContext == EmailActionContexts.ParticipationResponse
            && token.TargetType == EmailActionTargetTypes.VisitParticipant)
        {
            return await HandleParticipantAsync(token, result, cancellationToken);
        }

        if (token.ActionContext == EmailActionContexts.LogisticsAssigneeResponse
            && token.TargetType == EmailActionTargetTypes.LogisticsItem)
        {
            return await HandleLogisticsAssigneeAsync(token, result, cancellationToken);
        }

        return invalid;
    }

    private async Task<EmailActionInfoResult> HandleParticipantAsync(
        Domain.Entities.Emails.EmailActionToken token, EmailActionInfoResult result, CancellationToken cancellationToken)
    {
        var participant = await _db.VisitParticipants
            .FirstOrDefaultAsync(p => p.ParticipantId == token.TargetId, cancellationToken);
        if (participant is null)
            return new EmailActionInfoResult { Status = EmailActionViewStatuses.Invalid };

        // Determine the actionable state. (Token already consumed → already-responded; expired →
        // expired; the participant already responded another way → already-responded; else valid.)
        if (token.UsedAt != null || token.ResultStatus != EmailActionResultStatuses.Pending)
            result.Status = EmailActionViewStatuses.AlreadyResponded;
        else if (token.ExpiresAt < _clock.UtcNow)
            result.Status = EmailActionViewStatuses.Expired;
        else if (participant.Status != ParticipantStatuses.Invited)
            result.Status = EmailActionViewStatuses.AlreadyResponded;
        else
            result.Status = EmailActionViewStatuses.Valid;

        if (participant.Status == ParticipantStatuses.Accepted || participant.Status == ParticipantStatuses.Declined)
            result.CurrentResponse = EmailActionDisplay.ResponseLabel(participant.Status);

        // Display context (best-effort — never blocks rendering).
        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == participant.VisitInstanceId, cancellationToken);
        if (instance != null)
        {
            result.DelegationName = instance.VisitRequest?.DelegationName;
            result.PlannedTimeText = EmailActionDisplay.FormatWindow(instance.PlannedStartAt, instance.PlannedEndAt);
            result.CampusName = await _db.Campuses
                .Where(c => c.CampusId == instance.CampusId).Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
        result.RecipientName = await _db.Users
            .Where(u => u.UserId == participant.UserId).Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        result.ParticipantRoleLabel = EmailActionDisplay.RoleLabel(participant.ParticipantRole);

        return result;
    }

    private async Task<EmailActionInfoResult> HandleLogisticsAssigneeAsync(
        Domain.Entities.Emails.EmailActionToken token, EmailActionInfoResult result, CancellationToken cancellationToken)
    {
        var item = await _db.VisitLogisticsItems
            .FirstOrDefaultAsync(x => x.LogisticsItemId == token.TargetId, cancellationToken);
        if (item is null)
            return new EmailActionInfoResult { Status = EmailActionViewStatuses.Invalid };

        if (token.UsedAt != null || token.ResultStatus != EmailActionResultStatuses.Pending)
            result.Status = EmailActionViewStatuses.AlreadyResponded;
        else if (token.ExpiresAt < _clock.UtcNow)
            result.Status = EmailActionViewStatuses.Expired;
        else if (item.Status != LogisticsItemStatus.Assigned && item.Status != LogisticsItemStatus.Requested)
            result.Status = EmailActionViewStatuses.AlreadyResponded;
        else
            result.Status = EmailActionViewStatuses.Valid;

        if (item.Status == LogisticsItemStatus.Accepted || item.Status == LogisticsItemStatus.Rejected)
            result.CurrentResponse = item.Status == LogisticsItemStatus.Accepted ? "Đã xác nhận" : "Đã từ chối";

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == item.VisitInstanceId, cancellationToken);
        if (instance != null)
        {
            result.DelegationName = instance.VisitRequest?.DelegationName;
            result.PlannedTimeText = EmailActionDisplay.FormatWindow(instance.PlannedStartAt, instance.PlannedEndAt);
            result.CampusName = await _db.Campuses
                .Where(c => c.CampusId == instance.CampusId).Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
        result.RecipientName = token.RecipientUserId.HasValue
            ? await _db.Users.Where(u => u.UserId == token.RecipientUserId.Value).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;
        result.ParticipantRoleLabel = $"Yêu cầu: {item.Title}"; // Repurpose this field for display

        return result;
    }
}
