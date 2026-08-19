using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Common;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.DeclineInvitation
{
    public class DeclineInvitationCommand : IRequest<bool>
    {
        public ulong ParticipantId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// The department screens' entry point for declining a reception invitation. Like its Accept
    /// counterpart it no longer owns the transition — ownership, role, current status and the visit
    /// lifecycle are all enforced by <see cref="VisitInvitationResponse"/>, which the delegations screen
    /// calls too, so declining means the same thing wherever the button is.
    /// </summary>
    public class DeclineInvitationCommandHandler : IRequestHandler<DeclineInvitationCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
        private readonly IUserMutationLockService _lockService;

        public DeclineInvitationCommandHandler(
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

        public async Task<bool> Handle(DeclineInvitationCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
                throw new ForbiddenException();

            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new ValidationException("Vui lòng nhập lý do từ chối");

            var userId = _currentUserService.UserId.Value;
            var now = VietnamTime.Now();

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _lockService.LockUsersAsync(new[] { userId }, cancellationToken);

            var p = await VisitInvitationResponse.ApplyAsync(
                _context, userId, request.ParticipantId, accept: false, request.Reason, now, cancellationToken);

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
                        Message: $"{actorName} đã từ chối lời mời hỗ trợ đoàn {delegationName}.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.ParticipationResponded,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitParticipant,
                        RelatedId: p.ParticipantId,
                        ActorUserId: userId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Invitation,
                        VisitInstanceId: p.VisitInstanceId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: $"/dashboard/visit/process/{p.VisitInstanceId}",
                        MetadataJson: PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                            PEMS.Application.Notifications.Common.NotificationEventKeys.ParticipationDeclined,
                            new { delegationName, participantName = actorName, reason = request.Reason })),
                    cancellationToken
                );
            }

            return true;
        }
    }
}
