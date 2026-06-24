using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Application.Campuses.Queries.GetCampusFilterOptions;

/// <summary>
/// UC-83 handler. Returns the distinct cities + the campus list + the fixed status enum,
/// all sourced from the database so the HO screen's dropdowns never hard-code options.
/// HO/ADMIN only.
/// </summary>
public sealed class GetCampusFilterOptionsQueryHandler
    : IRequestHandler<GetCampusFilterOptionsQuery, CampusFilterOptionsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoleAccessPolicy _accessPolicy;

    public GetCampusFilterOptionsQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRoleAccessPolicy accessPolicy)
    {
        _db = db;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
    }

    public async Task<CampusFilterOptionsDto> Handle(GetCampusFilterOptionsQuery request, CancellationToken cancellationToken)
    {
        if (!_accessPolicy.CanAccessCampusManagement(_currentUser))
        {
            throw new AuthBusinessException(
                CampusErrorCodes.CampusManagementForbidden,
                "Bạn không có quyền xem danh sách campus.", 403);
        }

        var cities = await _db.Campuses.AsNoTracking()
            .Where(c => c.City != null && c.City != "")
            .Select(c => c.City!)
            .Distinct()
            .OrderBy(city => city)
            .ToListAsync(cancellationToken);

        var campuses = await _db.Campuses.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CampusFilterOption
            {
                CampusId = c.CampusId,
                CampusCode = c.CampusCode,
                Name = c.Name,
                City = c.City,
                Status = c.Status,
            })
            .ToListAsync(cancellationToken);

        return new CampusFilterOptionsDto
        {
            Cities = cities,
            Campuses = campuses,
            Statuses = new()
            {
                new CampusStatusOption { Value = EntityStatuses.Active, Label = "Hoạt động" },
                new CampusStatusOption { Value = EntityStatuses.Inactive, Label = "Ngừng hoạt động" },
            },
        };
    }
}
