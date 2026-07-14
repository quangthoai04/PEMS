using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;

namespace PEMS.Application.Campuses.Queries.ViewCampusDetails;

/// <summary>
/// UC-84 handler. HO/ADMIN only. Projects the full campus detail (master data + audit
/// names resolved via LEFT JOIN) plus the campus' IC department (department_type = 'IC',
/// nullable so a missing IC dept surfaces a UI warning rather than crashing — BR-84-03).
/// 404 when the campus does not exist.
/// </summary>
public sealed class ViewCampusDetailsQueryHandler : IRequestHandler<ViewCampusDetailsQuery, ViewCampusDetailsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoleAccessPolicy _accessPolicy;

    public ViewCampusDetailsQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRoleAccessPolicy accessPolicy)
    {
        _db = db;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
    }

    public async Task<ViewCampusDetailsDto> Handle(ViewCampusDetailsQuery request, CancellationToken cancellationToken)
    {
        if (!_accessPolicy.CanAccessCampusManagement(_currentUser))
        {
            throw new AuthBusinessException(
                CampusErrorCodes.CampusManagementForbidden,
                "Bạn không có quyền xem chi tiết campus.", 403);
        }

        var dto = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == request.CampusId)
            .Select(c => new ViewCampusDetailsDto
            {
                CampusId = c.CampusId,
                CampusCode = c.CampusCode,
                Name = c.Name,
                City = c.City,
                Address = c.Address,
                Phone = c.Phone,
                Email = c.Email,
                IcHeadUserId = c.IcHeadUserId,
                IcHeadName = c.IcHeadUser != null ? c.IcHeadUser.FullName : null,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                CreatedBy = c.CreatedBy,
                CreatedByName = _db.Users.Where(u => u.UserId == c.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
                UpdatedAt = c.UpdatedAt,
                UpdatedBy = c.UpdatedBy,
                UpdatedByName = c.UpdatedBy != null
                    ? _db.Users.Where(u => u.UserId == c.UpdatedBy).Select(u => u.FullName).FirstOrDefault()
                    : null,
                IcDepartment = _db.Departments
                    .Where(d => d.CampusId == c.CampusId && d.DepartmentType == "IC")
                    .OrderBy(d => d.DepartmentId)
                    .Select(d => new ViewCampusDetailsDto.IcDepartmentDetail
                    {
                        DepartmentId = d.DepartmentId,
                        Name = d.Name,
                        DepartmentType = d.DepartmentType,
                        Status = d.Status,
                        HeadUserId = d.HeadUserId,
                        HeadUserName = d.HeadUser != null ? d.HeadUser.FullName : null,
                    })
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Campus", request.CampusId);

        // Operational readiness (UC-86 §21) via the shared evaluator — same source as the
        // list badge, the registration dropdown and the submit guard.
        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(
            _db, dto.CampusId, cancellationToken);
        dto.Readiness = snapshot?.Readiness;

        return dto;
    }
}
