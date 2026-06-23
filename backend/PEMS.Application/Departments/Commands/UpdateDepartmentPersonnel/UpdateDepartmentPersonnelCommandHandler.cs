using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Departments.Commands.UpdateDepartmentPersonnel;

public sealed class UpdateDepartmentPersonnelCommandHandler : IRequestHandler<UpdateDepartmentPersonnelCommand, UpdateDepartmentPersonnelResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateDepartmentPersonnelCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<UpdateDepartmentPersonnelResponse> Handle(UpdateDepartmentPersonnelCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == request.UserId && u.DepartmentId == request.DepartmentId, cancellationToken);

        if (user == null)
        {
            return new UpdateDepartmentPersonnelResponse { Success = false, Message = "Nhân sự không tồn tại hoặc không thuộc phòng ban này." };
        }

        user.FullName = request.FullName;
        user.Phone = request.Phone;
        user.Gender = request.Gender;
        user.UpdatedBy = _currentUserService.UserId;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateDepartmentPersonnelResponse { Success = true, Message = "Đã cập nhật thông tin nhân sự thành công." };
    }
}
