using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;

namespace PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;

public sealed class RespondVisitParticipantInvitationCommandHandler
    : IRequestHandler<RespondVisitParticipantInvitationCommand, RespondVisitParticipantInvitationResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
    private readonly IUserMutationLockService _lockService;

    public RespondVisitParticipantInvitationCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        IUserMutationLockService lockService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notificationService = notificationService;
        _lockService = lockService;
    }

    public async Task<RespondVisitParticipantInvitationResponse> Handle(
        RespondVisitParticipantInvitationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        // Accepting turns a pending invitation into an active participation. Both already block a
        // role change, so this transition creates no new blocker — but the flow still joins the
        // shared lock protocol (see IUserMutationLockService) so every participant write in the
        // system serializes against role changes the same way (spec §14).
        await using var transaction = await _db.BeginSerializedTransactionAsync(cancellationToken);
        await _lockService.LockUsersAsync(new[] { userId }, cancellationToken);

        var now = _clock.VietnamNow;

        // Ownership, role, current status, lifecycle, the write itself and the audit entry all live in
        // the shared transition (see VisitInvitationResponse) — the department screens go through
        // exactly the same call, so one business action has one implementation. The validator
        // guarantees a non-empty 5–1000 char decline reason here.
        var participant = await VisitInvitationResponse.ApplyCoreAsync(
            _db, _lockService, userId, request.ParticipantId, requiredStatus: null,
            request.Accept, request.DeclineReason, request.Note, now, cancellationToken);
        var newStatus = participant.Status;

        // A Portal response retires any pending emailed link for this row (the token side effect is
        // channel-specific — see VisitInvitationResponse's class remarks).
        await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, PEMS.Domain.Constants.EmailActionTargetTypes.VisitParticipant, participant.ParticipantId,
            "Lời mời này đã được phản hồi.", now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (participant.VisitInstance?.CurrentHostUserId != null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            string participantName = user?.FullName ?? "Người được mời";
            // Mixed per-campus v2: notification text uses THIS instance's detail name.
            string delegationName = (await Services.VisitFormRead.VisitInstanceEffectiveName
                .ForInstancesAsync(_db, new[] { participant.VisitInstanceId }, cancellationToken))
                .GetValueOrDefault(participant.VisitInstanceId) ?? "Đoàn khách";
            string responseText = request.Accept ? "chấp nhận" : "từ chối";

            string metadataJson = request.Accept
                ? PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                    PEMS.Application.Notifications.Common.NotificationEventKeys.ParticipationAccepted,
                    new { delegationName, participantName })
                : PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                    PEMS.Application.Notifications.Common.NotificationEventKeys.ParticipationDeclined,
                    new { delegationName, participantName, reason = request.DeclineReason });

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
                    ActionUrl: $"/dashboard/visit/process/{participant.VisitInstanceId}",
                    MetadataJson: metadataJson),
                cancellationToken
            );
        }

        var message = request.Accept
            ? "Đã xác nhận tham gia. Đơn sẽ xuất hiện trong tab Đơn mời tham dự."
            : "Đã từ chối lời mời tham gia.";

        return new RespondVisitParticipantInvitationResponse(participant.ParticipantId, newStatus, message);
    }
}
