using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Campuses.Queries.ViewCampusList;

public sealed class ViewCampusListQueryHandler : IRequestHandler<ViewCampusListQuery, ViewCampusListDto>
{
    private readonly IApplicationDbContext _db;

    public ViewCampusListQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ViewCampusListDto> Handle(ViewCampusListQuery request, CancellationToken cancellationToken)
    {
        var campuses = await _db.Campuses
            .AsNoTracking()
            .Include(c => c.IcHeadUser)
            .OrderBy(c => c.CampusCode)
            .Select(c => new CampusItemDto
            {
                CampusId = c.CampusId,
                CampusCode = c.CampusCode,
                Name = c.Name,
                City = c.City,
                Address = c.Address,
                Phone = c.Phone,
                Email = c.Email,
                IcHeadUserId = c.IcHeadUserId,
                IcHeadUserName = c.IcHeadUser != null ? c.IcHeadUser.FullName : null,
                Status = c.Status
            })
            .ToListAsync(cancellationToken);

        return new ViewCampusListDto
        {
            Campuses = campuses
        };
    }
}