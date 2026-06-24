using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Departments.Queries.SearchPersonnel;

namespace PEMS.Application.Departments.Queries.ViewPersonnelDetails;

public sealed class ViewPersonnelDetailsQueryHandler : IRequestHandler<ViewPersonnelDetailsQuery, SearchPersonnelDto>
{
    private readonly IApplicationDbContext _context;

    public ViewPersonnelDetailsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SearchPersonnelDto> Handle(ViewPersonnelDetailsQuery request, CancellationToken cancellationToken)
    {
        var u = await _context.Users
            .Include(x => x.Role)
            .Include(x => x.PrimaryCampus)
            .FirstOrDefaultAsync(x => x.UserId == request.UserId && x.DepartmentId == request.DepartmentId, cancellationToken);

        if (u == null) return null;

        return new SearchPersonnelDto
        {
            Id = u.UserId.ToString(),
            Name = u.FullName,
            Email = u.Email,
            Phone = u.Phone,
            Status = u.Status == "ACTIVE" ? "Đã cấp tài khoản" : "Chưa cấp tài khoản",
            Role = u.SubRole == "LEADER" ? "Trưởng phòng" : "Nhân viên",
            Gender = u.Gender != null ? u.Gender.ToString() : "Khác",
            Campus = u.PrimaryCampus != null ? u.PrimaryCampus.Name : "",
            SystemRole = u.Role.RoleCode,
            AvatarUrl = u.AvatarUrl,
            RawStatus = u.Status
        };
    }
}
