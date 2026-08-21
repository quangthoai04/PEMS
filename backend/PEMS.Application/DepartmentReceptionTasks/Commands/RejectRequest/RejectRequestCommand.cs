using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

using PEMS.Application.Common;
namespace PEMS.Application.DepartmentReceptionTasks.Commands.RejectRequest
{
    public class RejectRequestCommand : IRequest<bool>
    {
        public ulong LogisticsItemId { get; set; }
        public string Reason { get; set; }
    }

    public class RejectRequestCommandHandler : IRequestHandler<RejectRequestCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserMutationLockService _lockService;

        public RejectRequestCommandHandler(
            IApplicationDbContext context, ICurrentUserService currentUserService,
            IUserMutationLockService lockService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _lockService = lockService;
        }

        public async Task<bool> Handle(RejectRequestCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason)) throw new Exception("Vui lòng nhập lý do từ chối");

            var visitInstanceId = await _context.VisitLogisticsItems
                .Where(x => x.LogisticsItemId == request.LogisticsItemId)
                .Select(x => (ulong?)x.VisitInstanceId)
                .FirstOrDefaultAsync(cancellationToken);
            if (visitInstanceId is null) throw new Exception("Không tìm thấy đơn yêu cầu");
            var visitRequestId = await _context.VisitRequestCampuses
                .Where(c => c.VisitInstanceId == visitInstanceId)
                .Select(c => (ulong?)c.VisitRequestId)
                .FirstOrDefaultAsync(cancellationToken) ?? throw new Exception("Không tìm thấy chuyến tiếp khách");

            // Lock hierarchy (see IUserMutationLockService) — this handler previously took no lock and
            // no transaction at all.
            await using var transaction = await _context.BeginSerializedTransactionAsync(cancellationToken);
            await _lockService.LockVisitRequestsAsync(new[] { visitRequestId }, cancellationToken);
            await _lockService.LockVisitRequestCampusesAsync(new[] { visitInstanceId.Value }, cancellationToken);
            await _lockService.LockVisitLogisticsItemsAsync(new[] { request.LogisticsItemId }, cancellationToken);

            var l = await _context.VisitLogisticsItems
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

            if (l == null) throw new Exception("Không tìm thấy đơn yêu cầu");

            ulong userId = _currentUserService.UserId.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null || l.RequestedToDepartmentId != user.DepartmentId)
                throw new Exception("Không có quyền từ chối đơn yêu cầu của phòng ban khác");

            //     throw new Exception("Trạng thái đơn yêu cầu không hợp lệ để từ chối");

            if (l.Status != "REQUESTED" && l.Status != "CHANGE_PROPOSED" && l.Status != "DECLINED")
                throw new Exception("Trạng thái đơn yêu cầu không hợp lệ để từ chối");

            l.Status = "REJECTED";
            l.DecisionNote = request.Reason;
            l.UpdatedBy = userId;
            l.UpdatedAt = VietnamTime.Now();

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }
}
