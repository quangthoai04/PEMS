using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

using PEMS.Application.Common;
namespace PEMS.Application.DepartmentReceptionTasks.Commands.AcceptInvitation
{
    public class AcceptInvitationCommand : IRequest<bool>
    {
        public ulong ParticipantId { get; set; }
    }

    public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

        public AcceptInvitationCommandHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            PEMS.Application.Notifications.Common.INotificationService notificationService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _notificationService = notificationService;
        }

        public async Task<bool> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
        {
            var p = await _context.VisitParticipants
                .Include(x => x.VisitInstance).ThenInclude(v => v.VisitRequest)
                .FirstOrDefaultAsync(x => x.ParticipantId == request.ParticipantId, cancellationToken);

            if (p == null) throw new Exception("Không tìm thấy thư mời");

            // Chỉ cho ACCEPT nếu status = INVITED
            // Allow accepting anytime
            // if (p.Status != "INVITED") throw new Exception("Thư mời không ở trạng thái chờ xác nhận.");

            var userId = _currentUserService.UserId;
            if (!userId.HasValue) throw new Exception("Không xác định được người dùng");

            // Database time conflict check
            var campus = await _context.VisitRequestCampuses.AsNoTracking()
                .FirstOrDefaultAsync(c => c.VisitInstanceId == p.VisitInstanceId, cancellationToken);
            if (campus != null)
            {
                bool hasConflict = await PEMS.Application.Common.Utils.ScheduleConflictChecker.HasConflictAsync(
                    _context, userId.Value, campus.PlannedStartAt, campus.PlannedEndAt, null, p.ParticipantId, cancellationToken);
                if (hasConflict)
                {
                    throw new Exception("Thư mời này đã trùng thời gian với công việc khác của bạn. Hãy phân công cho nhân sự khác.");
                }
            }

            p.Status = "ACCEPTED";
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
                        Message: $"{actorName} đã chấp nhận lời mời hỗ trợ đoàn {delegationName}.",
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
