using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
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
    private readonly IVisitFormReadService _formReadService;

    public GetEmailActionInfoQueryHandler(
        IApplicationDbContext db, IDateTimeService clock, IEmailActionTokenService tokens,
        IVisitFormReadService formReadService)
    {
        _db = db;
        _clock = clock;
        _tokens = tokens;
        _formReadService = formReadService;
    }

    // The landing page is bound to ONE campus instance (token → participant/logistics item → instance), so it
    // shows THAT instance's own delegation name — never a sibling's, and never a request-level value, which
    // does not exist. A missing detail surfaces as the standard 409 instead of a blank.
    private async Task<string?> ResolveDelegationNameAsync(VisitRequestCampus instance, CancellationToken ct)
    {
        var visit = instance.VisitRequest;
        if (visit is null) return null;
        var content = await _formReadService.ResolveCampusFormContentAsync(
            visit, new[] { instance.VisitInstanceId }, ct);
        return content[instance.VisitInstanceId].DelegationName;
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

        var result = new EmailActionInfoResult { Action = token.IntendedAction, Context = token.ActionContext };

        // v2 identity claim/transfer links are NEVER served by the anonymous email-action flow
        // (plan §4.4): those pages require a logged-in session whose email matches the invitation —
        // the dedicated /api/public/visit-contact-{claims|transfers}/{token} + /api/v2 flows.
        if (token.ActionContext == EmailActionContexts.VisitContactClaim
            || token.ActionContext == EmailActionContexts.VisitContactTransfer)
            return invalid;

        if (token.ActionContext == EmailActionContexts.ParticipationResponse
            && token.TargetType == EmailActionTargetTypes.VisitParticipant)
        {
            // A direct invitation only ever starts respondable at INVITED.
            return await HandleParticipantAsync(token, result, ParticipantStatuses.Invited, cancellationToken);
        }

        if (token.ActionContext == EmailActionContexts.ParticipationAssignmentResponse
            && token.TargetType == EmailActionTargetTypes.VisitParticipant)
        {
            // A Department-Staff delegation is minted only once the row is already ASSIGNED, and only
            // that status is valid for this context — the opposite of the direct-invitation case above.
            return await HandleParticipantAsync(token, result, ParticipantStatuses.Assigned, cancellationToken);
        }

        if (token.ActionContext == EmailActionContexts.LogisticsRequestResponse
            && token.TargetType == EmailActionTargetTypes.LogisticsItem)
        {
            return await HandleLogisticsRequestAsync(token, result, cancellationToken);
        }

        if (token.ActionContext == EmailActionContexts.LogisticsAssigneeResponse
            && token.TargetType == EmailActionTargetTypes.LogisticsItem)
        {
            return await HandleLogisticsAssigneeAsync(token, result, cancellationToken);
        }

        return invalid;
    }

    private async Task<EmailActionInfoResult> HandleParticipantAsync(
        Domain.Entities.Emails.EmailActionToken token, EmailActionInfoResult result,
        string requiredStatus, CancellationToken cancellationToken)
    {
        var participant = await _db.VisitParticipants
            .FirstOrDefaultAsync(p => p.ParticipantId == token.TargetId, cancellationToken);
        if (participant is null)
            return new EmailActionInfoResult { Status = EmailActionViewStatuses.Invalid };

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == participant.VisitInstanceId, cancellationToken);

        if (instance != null)
        {
            result.DelegationName = await ResolveDelegationNameAsync(instance, cancellationToken);
            result.PlannedTimeText = EmailActionDisplay.FormatWindow(instance.PlannedStartAt, instance.PlannedEndAt);
            result.CampusName = await _db.Campuses
                .Where(c => c.CampusId == instance.CampusId).Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
        result.RecipientName = await _db.Users
            .Where(u => u.UserId == participant.UserId).Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        result.ParticipantRoleLabel = EmailActionDisplay.RoleLabel(participant.ParticipantRole);

        // State machine validation
        if (token.ResultStatus == EmailActionResultStatuses.Invalid)
        {
            result.Status = EmailActionViewStatuses.Invalid;
        }
        else if (token.ResultStatus == EmailActionResultStatuses.Expired || token.ExpiresAt < _clock.VietnamNow)
        {
            result.Status = EmailActionViewStatuses.Expired;
        }
        else if (token.ResultStatus == EmailActionResultStatuses.AlreadyResponded || token.UsedAt != null || token.ResultStatus == EmailActionResultStatuses.Success)
        {
            result.Status = EmailActionViewStatuses.AlreadyResponded;
        }
        else if (instance == null || instance.Status == VisitInstanceStatus.Cancelled || instance.Status == VisitInstanceStatus.Closed)
        {
            result.Status = EmailActionViewStatuses.Invalid; // Parent invalid
        }
        else if (participant.Status == ParticipantStatuses.Removed)
        {
            result.Status = EmailActionViewStatuses.Invalid;
        }
        else if (participant.Status == ParticipantStatuses.Accepted || participant.Status == ParticipantStatuses.Declined)
        {
            result.Status = EmailActionViewStatuses.AlreadyResponded;
        }
        else if (participant.Status != requiredStatus)
        {
            // A direct-invite token seeing ASSIGNED (delegated meanwhile), or an assignment token
            // seeing INVITED (should never happen — assignment tokens are only minted once the row is
            // already ASSIGNED), is invalid: the token no longer describes what the row actually is.
            result.Status = EmailActionViewStatuses.Invalid;
        }
        else
        {
            result.Status = EmailActionViewStatuses.Valid;
        }

        if (participant.Status == ParticipantStatuses.Accepted || participant.Status == ParticipantStatuses.Declined)
            result.CurrentResponse = EmailActionDisplay.ResponseLabel(participant.Status);

        return result;
    }

    private async Task<EmailActionInfoResult> HandleLogisticsRequestAsync(
        Domain.Entities.Emails.EmailActionToken token, EmailActionInfoResult result, CancellationToken cancellationToken)
    {
        var item = await _db.VisitLogisticsItems
            .FirstOrDefaultAsync(x => x.LogisticsItemId == token.TargetId, cancellationToken);
        if (item is null)
            return new EmailActionInfoResult { Status = EmailActionViewStatuses.Invalid };

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == item.VisitInstanceId, cancellationToken);
        if (instance != null)
        {
            result.DelegationName = await ResolveDelegationNameAsync(instance, cancellationToken);
            result.PlannedTimeText = EmailActionDisplay.FormatWindow(instance.PlannedStartAt, instance.PlannedEndAt);
            result.CampusName = await _db.Campuses
                .Where(c => c.CampusId == instance.CampusId).Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
        result.RecipientName = token.RecipientUserId.HasValue
            ? await _db.Users.Where(u => u.UserId == token.RecipientUserId.Value).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;
        result.ParticipantRoleLabel = item.Title;   // shown as "Hạng mục" on the logistics landing

        if (token.ResultStatus == EmailActionResultStatuses.Invalid)
        {
            result.Status = EmailActionViewStatuses.Invalid;
        }
        else if (token.ResultStatus == EmailActionResultStatuses.Expired || token.ExpiresAt < _clock.VietnamNow)
        {
            result.Status = EmailActionViewStatuses.Expired;
        }
        else if (token.ResultStatus == EmailActionResultStatuses.AlreadyResponded || token.UsedAt != null || token.ResultStatus == EmailActionResultStatuses.Success)
        {
            result.Status = EmailActionViewStatuses.AlreadyResponded;
        }
        else if (instance == null || instance.Status == VisitInstanceStatus.Cancelled || instance.Status == VisitInstanceStatus.Closed)
        {
            result.Status = EmailActionViewStatuses.Invalid;
        }
        else if (item.Status != LogisticsItemStatus.Requested)
        {
            if (item.Status == LogisticsItemStatus.Assigned || item.Status == LogisticsItemStatus.Accepted || item.Status == LogisticsItemStatus.Done)
                result.Status = EmailActionViewStatuses.AlreadyResponded;
            else
                result.Status = EmailActionViewStatuses.Invalid;
        }
        else
        {
            result.Status = EmailActionViewStatuses.Valid;
        }

        if (item.Status == LogisticsItemStatus.Assigned || item.Status == LogisticsItemStatus.Accepted || item.Status == LogisticsItemStatus.Done)
            result.CurrentResponse = "Đã tiếp nhận";
        else if (item.Status == LogisticsItemStatus.Rejected || item.Status == LogisticsItemStatus.Cancelled)
            result.CurrentResponse = "Đã từ chối / hủy";

        return result;
    }

    private async Task<EmailActionInfoResult> HandleLogisticsAssigneeAsync(
        Domain.Entities.Emails.EmailActionToken token, EmailActionInfoResult result, CancellationToken cancellationToken)
    {
        var item = await _db.VisitLogisticsItems
            .FirstOrDefaultAsync(x => x.LogisticsItemId == token.TargetId, cancellationToken);
        if (item is null)
            return new EmailActionInfoResult { Status = EmailActionViewStatuses.Invalid };

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == item.VisitInstanceId, cancellationToken);
        if (instance != null)
        {
            result.DelegationName = await ResolveDelegationNameAsync(instance, cancellationToken);
            result.PlannedTimeText = EmailActionDisplay.FormatWindow(instance.PlannedStartAt, instance.PlannedEndAt);
            result.CampusName = await _db.Campuses
                .Where(c => c.CampusId == instance.CampusId).Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
        result.RecipientName = token.RecipientUserId.HasValue
            ? await _db.Users.Where(u => u.UserId == token.RecipientUserId.Value).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;
        result.ParticipantRoleLabel = $"Yêu cầu: {item.Title}"; // Repurpose this field for display

        if (token.ResultStatus == EmailActionResultStatuses.Invalid)
        {
            result.Status = EmailActionViewStatuses.Invalid;
        }
        else if (token.ResultStatus == EmailActionResultStatuses.Expired || token.ExpiresAt < _clock.VietnamNow)
        {
            result.Status = EmailActionViewStatuses.Expired;
        }
        else if (token.ResultStatus == EmailActionResultStatuses.AlreadyResponded || token.UsedAt != null || token.ResultStatus == EmailActionResultStatuses.Success)
        {
            result.Status = EmailActionViewStatuses.AlreadyResponded;
        }
        else if (instance == null || instance.Status == VisitInstanceStatus.Cancelled || instance.Status == VisitInstanceStatus.Closed)
        {
            result.Status = EmailActionViewStatuses.Invalid;
        }
        else if (item.Status == LogisticsItemStatus.Cancelled || item.Status == LogisticsItemStatus.Rejected || item.Status == LogisticsItemStatus.Done)
        {
            result.Status = EmailActionViewStatuses.Invalid;
        }
        else if (item.Status == LogisticsItemStatus.Accepted || item.Status == LogisticsItemStatus.Declined)
        {
            result.Status = EmailActionViewStatuses.AlreadyResponded;
        }
        else if (item.Status == LogisticsItemStatus.Assigned && item.AssignedToUserId != token.RecipientUserId)
        {
            result.Status = EmailActionViewStatuses.Invalid;
        }
        else if (item.Status != LogisticsItemStatus.Assigned)
        {
            result.Status = EmailActionViewStatuses.Invalid;
        }
        else
        {
            result.Status = EmailActionViewStatuses.Valid;
        }

        if (item.Status == LogisticsItemStatus.Accepted || item.Status == LogisticsItemStatus.Declined)
            result.CurrentResponse = item.Status == LogisticsItemStatus.Accepted ? "Đã xác nhận" : "Đã từ chối";

        return result;
    }
}
