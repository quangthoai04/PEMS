using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Delegations;
using System;
using System.Threading;
using System.Threading.Tasks;

using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.ConfirmRequest
{
    public class ConfirmRequestCommand : IRequest<bool>
    {
        public ulong LogisticsItemId { get; set; }
    }

    public class ConfirmRequestCommandHandler : IRequestHandler<ConfirmRequestCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserMutationLockService _lockService;

        public ConfirmRequestCommandHandler(
            IApplicationDbContext context, ICurrentUserService currentUserService,
            IUserMutationLockService lockService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _lockService = lockService;
        }

        public async Task<bool> Handle(ConfirmRequestCommand request, CancellationToken cancellationToken)
        {
            ulong userId = _currentUserService.UserId.Value;

            // Taking the item makes the caller its assignee — a live responsibility. The shared lock
            // (see IUserMutationLockService) serializes that against a Staff Leader changing this
            // very account's role at the same moment.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _lockService.LockUsersAsync(new[] { userId }, cancellationToken);

            var l = await _context.VisitLogisticsItems
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

            if (l == null) throw new NotFoundException("Không tìm thấy đơn yêu cầu");

            // Verify department — read AFTER the lock, so a just-committed role/department move is seen.
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null || l.RequestedToDepartmentId != user.DepartmentId)
                throw new ForbiddenException("Không có quyền xác nhận đơn yêu cầu của phòng ban khác");

            // Database time conflict check
            var campus = await _context.VisitRequestCampuses.AsNoTracking()
                .FirstOrDefaultAsync(c => c.VisitInstanceId == l.VisitInstanceId, cancellationToken);
            DateTime startAt = l.UsageStartAt ?? (campus != null ? campus.PlannedStartAt : DateTime.MinValue);
            DateTime endAt = l.UsageEndAt ?? (campus != null ? campus.PlannedEndAt : DateTime.MaxValue);

            if (startAt != DateTime.MinValue && endAt != DateTime.MaxValue)
            {
                bool hasConflict = await PEMS.Application.Common.Utils.ScheduleConflictChecker.HasConflictAsync(
                    _context, userId, startAt, endAt, l.LogisticsItemId, null, cancellationToken);
                if (hasConflict)
                {
                    throw new ValidationException("Đơn này đã trùng thời gian với công việc khác của bạn. Hãy phân công cho nhân sự khác.");
                }
            }

            var now = VietnamTime.Now();

            l.Status = "ACCEPTED";
            l.ReceivedBy ??= userId;
            l.ReceivedAt ??= now;
            l.AssignedToUserId = userId;
            l.AssignedBy = userId;
            l.AssignedAt = now;
            l.AssigneeAcceptedAt = now;
            l.UpdatedBy = userId;
            l.UpdatedAt = now;

            _context.VisitLogisticsAssignmentAttempts.Add(new VisitLogisticsAssignmentAttempt
            {
                LogisticsItemId = request.LogisticsItemId,
                AssigneeUserId = userId,
                AssignedBy = userId,
                AssignedAt = now,
                Status = "ACCEPTED",
                RespondedAt = now,
                ResponseSource = "PORTAL",
                CreatedAt = now
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;
        }
    }
}
