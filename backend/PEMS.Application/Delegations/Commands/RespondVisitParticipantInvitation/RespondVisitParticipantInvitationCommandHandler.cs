using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;

public sealed class RespondVisitParticipantInvitationCommandHandler
    : IRequestHandler<RespondVisitParticipantInvitationCommand, RespondVisitParticipantInvitationResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public RespondVisitParticipantInvitationCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock, PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notificationService = notificationService;
    }

    public async Task<RespondVisitParticipantInvitationResponse> Handle(
        RespondVisitParticipantInvitationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var participant = await _db.VisitParticipants
            .Include(p => p.VisitInstance).ThenInclude(v => v.VisitRequest)
            .FirstOrDefaultAsync(p => p.ParticipantId == request.ParticipantId, cancellationToken)
            ?? throw new NotFoundException("VisitParticipant", request.ParticipantId);

        // Ownership: a user may only respond to their OWN invitation.
        if (participant.UserId != userId)
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

        var requestStatus = participant.VisitInstance?.VisitRequest?.Status;
        var campusStatus = participant.VisitInstance?.Status;

        var actionName = request.Accept ? "xác nhận tham gia" : "từ chối";

        if (requestStatus == VisitRequestStatuses.Cancelled || requestStatus == VisitRequestStatuses.Rejected
            || campusStatus == VisitInstanceStatuses.Cancelled || campusStatus == VisitInstanceStatuses.Rejected)
            throw new ConflictException($"Không thể {actionName} vì lịch thăm đã bị hủy hoặc từ chối.");

        if (campusStatus != VisitInstanceStatuses.Assigned && campusStatus != VisitInstanceStatuses.BeforeVisit)
            throw new ConflictException($"Không thể {actionName} vì chuyến thăm đã bắt đầu hoặc kết thúc.");

        var now = _clock.UtcNow;
        string newStatus;

        if (request.Accept)
        {
            participant.Status = ParticipantStatuses.Accepted;
            newStatus = ParticipantStatuses.Accepted;
        }
        else
        {
            participant.Status = ParticipantStatuses.Declined;
            // No decline_reason column on visit_participants — record it on note (per schema).
            // Validator guarantees a non-empty 5–1000 char reason here; store it trimmed.
            participant.Note = request.DeclineReason!.Trim();
            newStatus = ParticipantStatuses.Declined;
        }

        participant.RespondedAt = now;
        participant.UpdatedAt = now;
        participant.UpdatedBy = userId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            Action = request.Accept ? "ACCEPT_VISIT_INVITATION" : "DECLINE_VISIT_INVITATION",
            EntityType = "VisitParticipant",
            EntityId = participant.ParticipantId,
            CreatedAt = now
        });

        await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, EmailActionTargetTypes.VisitParticipant, participant.ParticipantId, "Lời mời này đã được phản hồi.", now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        if (participant.VisitInstance?.CurrentHostUserId != null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            string participantName = user?.FullName ?? "Người được mời";
            string delegationName = participant.VisitInstance.VisitRequest?.DelegationName ?? "Đoàn khách";
            string responseText = request.Accept ? "chấp nhận" : "từ chối";
            
            await _notificationService.CreateAsync(
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: participant.VisitInstance.CurrentHostUserId.Value,
                    Title: "Phản hồi lời mời tham gia",
                    Message: $"{participantName} đã {responseText} lời mời hỗ trợ đoàn {delegationName}.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.ParticipationResponded,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitParticipant,
                    RelatedId: participant.ParticipantId,
                    ActorUserId: userId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Invitation,
                    VisitInstanceId: participant.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit/process/{participant.VisitInstanceId}"),
                cancellationToken
            );
        }

        var message = request.Accept
            ? "Đã xác nhận tham gia. Đơn sẽ xuất hiện trong tab Đơn mời tham dự."
            : "Đã từ chối lời mời tham gia.";

        return new RespondVisitParticipantInvitationResponse(participant.ParticipantId, newStatus, message);
    }
}
