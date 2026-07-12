using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

using PEMS.Application.Common;
namespace PEMS.Application.Departments.Commands.RemovePersonnel;

public sealed class RemovePersonnelCommandHandler : IRequestHandler<RemovePersonnelCommand, RemovePersonnelResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RemovePersonnelCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<RemovePersonnelResponse> Handle(RemovePersonnelCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId && u.DepartmentId == request.DepartmentId, cancellationToken);
        if (user == null) return new RemovePersonnelResponse { Success = false, Message = "Không tìm thấy nhân sự." };

        var dept = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, cancellationToken);
        
        if (user.UserId == _currentUserService.UserId)
        {
            return new RemovePersonnelResponse { Success = false, Message = "Không thể tự gỡ chính mình." };
        }

        if (dept != null && dept.HeadUserId == user.UserId)
        {
            return new RemovePersonnelResponse { Success = false, Message = "Không thể gỡ trưởng phòng hiện tại khi chưa đổi trưởng phòng." };
        }

        if (user.Status == "ACTIVE")
        {
            user.Status = "INACTIVE";
        }
        else
        {
            user.Status = "ACTIVE";
        }

        user.UpdatedAt = VietnamTime.Now();
        user.UpdatedBy = _currentUserService.UserId;

        await _context.SaveChangesAsync(cancellationToken);

        return new RemovePersonnelResponse { Success = true, Message = user.Status == "ACTIVE" ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản." };
    }
}
