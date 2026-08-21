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

            // Structural FKs, safe to peek unlocked (see VisitInvitationResponse.ApplyCoreAsync).
            var visitInstanceId = await _context.VisitLogisticsItems
                .Where(x => x.LogisticsItemId == request.LogisticsItemId)
                .Select(x => (ulong?)x.VisitInstanceId)
                .FirstOrDefaultAsync(cancellationToken);
            if (visitInstanceId is null) throw new NotFoundException("Không tìm thấy đơn yêu cầu");
            var visitRequestId = await _context.VisitRequestCampuses
                .Where(c => c.VisitInstanceId == visitInstanceId)
                .Select(c => (ulong?)c.VisitRequestId)
                .FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Không tìm thấy chuyến tiếp khách");

            // Taking the item makes the caller its assignee — a live responsibility. The shared lock
            // (see IUserMutationLockService) serializes that against a Staff Leader changing this
            // very account's role at the same moment. Tiers 2-4 (VisitRequest → VisitRequestCampus →
            // VisitLogisticsItem) join the same hierarchy every writer of this aggregate follows.
            await using var transaction = await _context.BeginSerializedTransactionAsync(cancellationToken);
            await _lockService.LockUsersAsync(new[] { userId }, cancellationToken);
            await _lockService.LockVisitRequestsAsync(new[] { visitRequestId }, cancellationToken);
            await _lockService.LockVisitRequestCampusesAsync(new[] { visitInstanceId.Value }, cancellationToken);
            await _lockService.LockVisitLogisticsItemsAsync(new[] { request.LogisticsItemId }, cancellationToken);

            var l = await _context.VisitLogisticsItems
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

            if (l == null) throw new NotFoundException("Không tìm thấy đơn yêu cầu");

            // Verify department — read AFTER the lock, so a just-committed role/department move is seen.
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null || l.RequestedToDepartmentId != user.DepartmentId)
                throw new ForbiddenException("Không có quyền xác nhận đơn yêu cầu của phòng ban khác");

            // Đơn yêu cầu không bị chặn bởi các đơn yêu cầu hoặc thư mời khác trùng thời gian

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
