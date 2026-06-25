using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Departments.Common;

namespace PEMS.Application.Departments.Queries.ViewDepartmentDetails;

/// <summary>
/// UC-105 handler. Loads a department by id and enforces the Staff Leader campus scope:
/// missing → 404; in another campus → 403 (BR-105-02). canEditName / canToggleStatus are true
/// only for GENERAL departments (IC is view-only — BR-105-06/07).
/// </summary>
public sealed class ViewDepartmentDetailsQueryHandler
    : IRequestHandler<ViewDepartmentDetailsQuery, ViewDepartmentDetailsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ViewDepartmentDetailsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ViewDepartmentDetailsDto> Handle(
        ViewDepartmentDetailsQuery request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderDepartmentScope.EnsureStaffLeaderCampus(_currentUser);

        var row = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == request.DepartmentId)
            .Select(d => new
            {
                d.DepartmentId,
                d.Name,
                d.CampusId,
                CampusCode = d.Campus.CampusCode,
                CampusName = d.Campus.Name,
                d.HeadUserId,
                HeadFullName = d.HeadUser != null ? d.HeadUser.FullName : null,
                d.Status,
                d.DepartmentType,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Department", request.DepartmentId);

        if (row.CampusId != campusId)
            throw new AuthBusinessException(
                DepartmentErrorCodes.DepartmentScopeForbidden,
                "Bạn không có quyền xem phòng ban này.", 403);

        var isGeneral = row.DepartmentType == "GENERAL";

        return new ViewDepartmentDetailsDto
        {
            DepartmentId = row.DepartmentId,
            Name = row.Name,
            CampusId = row.CampusId,
            CampusCode = row.CampusCode,
            CampusName = row.CampusName,
            HeadUserId = row.HeadUserId,
            HeadFullName = row.HeadFullName,
            Status = row.Status,
            DepartmentType = row.DepartmentType,
            CanEditName = isGeneral,
            CanToggleStatus = isGeneral,
        };
    }
}
