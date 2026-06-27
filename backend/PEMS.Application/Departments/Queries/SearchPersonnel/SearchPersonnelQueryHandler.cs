using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Departments.Queries.SearchPersonnel;

public sealed class SearchPersonnelQueryHandler : IRequestHandler<SearchPersonnelQuery, SearchPersonnelResult>
{
    private readonly IApplicationDbContext _context;

    public SearchPersonnelQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SearchPersonnelResult> Handle(SearchPersonnelQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Users
            .Include(u => u.Role)
            .Include(u => u.PrimaryCampus)
            .Where(u => u.DepartmentId == request.DepartmentId && u.Role.RoleCode == "DEPARTMENT");

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(kw) || u.Email.ToLower().Contains(kw) || (u.Phone != null && u.Phone.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (request.Status.ToLower() == "active")
            {
                query = query.Where(u => u.Status == "ACTIVE");
            }
            else if (request.Status.ToLower() == "inactive")
            {
                query = query.Where(u => u.Status == "INACTIVE" || u.Status == "LOCK");
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(u => u.SubRole == "LEADER" ? 0 : 1).ThenBy(u => u.FullName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new SearchPersonnelDto
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
            })
            .ToListAsync(cancellationToken);

        return new SearchPersonnelResult
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
