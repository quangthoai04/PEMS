using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Common;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.AcceptInvitation
{
    public class AcceptInvitationCommand : IRequest<bool>
    {
        public ulong ParticipantId { get; set; }
    }

    /// <summary>
    /// The department screens' entry point for accepting a reception invitation.
    ///
    /// <para>
    /// It no longer owns the transition. This handler used to load the participant by id alone and write
    /// <c>Status = "ACCEPTED"</c> with both status guards commented out — so posting a colleague's
    /// participant id accepted on their behalf, an already-declined invitation could be flipped back, and
    /// a cancelled visit could still be joined. The identical action on the delegations screen enforced
    /// all three. The rules now live in <see cref="VisitInvitationResponse"/> and both call it.
    /// </para>
    /// <para>
    /// What stays here is what belongs to THIS screen: the department calendar's double-booking check,
    /// which the delegations flow has no equivalent of and which is a pre-condition of the click rather
    /// than part of the state transition.
    /// </para>
    /// </summary>
    public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
        private readonly IUserMutationLockService _lockService;

        public AcceptInvitationCommandHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            PEMS.Application.Notifications.Common.INotificationService notificationService,
            IUserMutationLockService lockService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _notificationService = notificationService;
            _lockService = lockService;
        }

        public async Task<bool> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
                throw new ForbiddenException();

            var userId = _currentUserService.UserId.Value;
            var now = VietnamTime.Now();

            // Same lock protocol as every other participant write (see IUserMutationLockService), so an
            // accept cannot interleave with a role change on the same account.
            await using var transaction = await _context.BeginSerializedTransactionAsync(cancellationToken);
            await _lockService.LockUsersAsync(new[] { userId }, cancellationToken);

            // Screen-specific pre-check: this calendar knows what else the caller has already agreed to,
            // and accepting two overlapping receptions is a commitment they cannot keep. Ownership is
            // still checked inside ApplyAsync — this only reads the caller's own schedule.
            var participantInstanceId = await _context.VisitParticipants
                .Where(p => p.ParticipantId == request.ParticipantId)
                .Select(p => (ulong?)p.VisitInstanceId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("VisitParticipant", request.ParticipantId);

            var campus = await _context.VisitRequestCampuses.AsNoTracking()
                .FirstOrDefaultAsync(c => c.VisitInstanceId == participantInstanceId, cancellationToken);
            if (campus != null)
            {
                bool hasConflict = await PEMS.Application.Common.Utils.ScheduleConflictChecker.HasInvitationConflictAsync(
                    _context, userId, campus.PlannedStartAt, campus.PlannedEndAt, request.ParticipantId, cancellationToken);
                if (hasConflict)
                {
                    throw new ValidationException("Thư mời này đã trùng thời gian với thư mời khác của bạn.");
                }
            }

            var p = await VisitInvitationResponse.ApplyCoreAsync(
                _context, _lockService, userId, request.ParticipantId, requiredStatus: null,
                accept: true, declineReason: null, note: null, now, cancellationToken);

            // A Portal response retires any pending emailed link for this row (the token side effect is
            // channel-specific — see VisitInvitationResponse's class remarks).
            await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                _context, PEMS.Domain.Constants.EmailActionTargetTypes.VisitParticipant, p.ParticipantId,
                "Lời mời này đã được phản hồi.", now, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (p.VisitInstance?.CurrentHostUserId != null)
            {
                var actorName = await _context.Users
                    .Where(u => u.UserId == userId)
                    .Select(u => u.FullName)
                    .FirstOrDefaultAsync(cancellationToken) ?? "Phòng ban";
                // Mixed per-campus v2: notification text uses THIS instance's detail name.
                var delegationName = (await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
                    .ForInstancesAsync(_context, new[] { p.VisitInstanceId }, cancellationToken))
                    .GetValueOrDefault(p.VisitInstanceId) ?? "Đoàn khách";

                await _notificationService.CreateAsync(
                    new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: p.VisitInstance.CurrentHostUserId.Value,
                        Title: "Phản hồi lời mời tham gia",
                        Message: $"{actorName} đã chấp nhận lời mời hỗ trợ đoàn {delegationName}.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.ParticipationResponded,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitParticipant,
                        RelatedId: p.ParticipantId,
                        ActorUserId: userId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Invitation,
                        VisitInstanceId: p.VisitInstanceId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: $"/dashboard/visit/process/{p.VisitInstanceId}",
                        MetadataJson: PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                            PEMS.Application.Notifications.Common.NotificationEventKeys.ParticipationAccepted,
                            new { delegationName, participantName = actorName })),
                    cancellationToken
                );
            }

            return true;
        }
    }
}
