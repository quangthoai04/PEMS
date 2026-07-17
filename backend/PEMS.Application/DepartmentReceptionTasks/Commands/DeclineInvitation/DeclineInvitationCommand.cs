using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

using PEMS.Application.Common;
namespace PEMS.Application.DepartmentReceptionTasks.Commands.DeclineInvitation
{
    public class DeclineInvitationCommand : IRequest<bool>
    {
        public ulong ParticipantId { get; set; }
        public string Reason { get; set; }
    }

    public class DeclineInvitationCommandHandler : IRequestHandler<DeclineInvitationCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

        public DeclineInvitationCommandHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            PEMS.Application.Notifications.Common.INotificationService notificationService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _notificationService = notificationService;
        }

        public async Task<bool> Handle(DeclineInvitationCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason)) throw new Exception("Vui lòng nhập lý do từ chối");

            var p = await _context.VisitParticipants
                .Include(x => x.VisitInstance).ThenInclude(v => v.VisitRequest)
                .FirstOrDefaultAsync(x => x.ParticipantId == request.ParticipantId, cancellationToken);

            if (p == null) throw new Exception("Không tìm thấy thư mời");

            // Allow declining anytime
            // if (p.Status != "INVITED") throw new Exception("Thư mời không ở trạng thái chờ xác nhận.");

            var userId = _currentUserService.UserId;
            p.Status = "DECLINED";
            p.Note = request.Reason;
            p.RespondedAt = VietnamTime.Now();
            p.UpdatedBy = userId;
            p.UpdatedAt = VietnamTime.Now();

            await _context.SaveChangesAsync(cancellationToken);

            if (p.VisitInstance?.CurrentHostUserId != null && userId.HasValue)
            {
                var actorName = await _context.Users
                    .Where(u => u.UserId == userId.Value)
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
                        ActorUserId: userId.Value,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Invitation,
                        VisitInstanceId: p.VisitInstanceId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: $"/dashboard/visit/process/{p.VisitInstanceId}"),
                    cancellationToken
                );
            }

            return true;
        }
    }
}
