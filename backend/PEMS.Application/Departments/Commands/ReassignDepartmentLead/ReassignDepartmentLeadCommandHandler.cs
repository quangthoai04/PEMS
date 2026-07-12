using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

using PEMS.Application.Common;
namespace PEMS.Application.Departments.Commands.ReassignDepartmentLead;

public sealed class ReassignDepartmentLeadCommandHandler : IRequestHandler<ReassignDepartmentLeadCommand, ReassignDepartmentLeadResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ReassignDepartmentLeadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ReassignDepartmentLeadResponse> Handle(ReassignDepartmentLeadCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            var dept = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, cancellationToken);
            if (dept == null) return new ReassignDepartmentLeadResponse { Success = false, Message = "Phòng ban không tồn tại." };

            var newLeader = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.NewLeaderUserId && u.DepartmentId == request.DepartmentId && u.Status == "ACTIVE", cancellationToken);
            if (newLeader == null) return new ReassignDepartmentLeadResponse { Success = false, Message = "Người được chọn không hợp lệ." };

            var oldLeaderId = dept.HeadUserId;

            if (oldLeaderId != request.NewLeaderUserId)
            {
                if (oldLeaderId.HasValue)
                {
                    var oldLeader = await _context.Users.FirstOrDefaultAsync(u => u.UserId == oldLeaderId.Value, cancellationToken);
                    if (oldLeader != null && oldLeader.DepartmentId == request.DepartmentId)
                    {
                        oldLeader.SubRole = "STAFF";
                        oldLeader.UpdatedAt = VietnamTime.Now();
                        oldLeader.UpdatedBy = _currentUserService.UserId;
                    }
                }

                newLeader.SubRole = "LEADER";
                newLeader.UpdatedAt = VietnamTime.Now();
                newLeader.UpdatedBy = _currentUserService.UserId;

                dept.HeadUserId = newLeader.UserId;
                dept.UpdatedAt = VietnamTime.Now();
                dept.UpdatedBy = _currentUserService.UserId;

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return new ReassignDepartmentLeadResponse { Success = true, Message = "Đã thay đổi trưởng phòng thành công." };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ReassignDepartmentLeadResponse { Success = false, Message = "Lỗi khi đổi trưởng phòng: " + ex.Message };
        }
    }
}
