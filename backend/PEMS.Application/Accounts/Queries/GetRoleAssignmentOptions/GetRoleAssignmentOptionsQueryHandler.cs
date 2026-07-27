using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Queries.GetRoleAssignmentOptions;

/// <summary>
/// UC-100-SL handler. Resolves the campus (from the authenticated Staff Leader), verifies the
/// target account is in that campus and is not the caller, then returns the campus IC department
/// and the active GENERAL departments for the role-edit dropdowns. Any client-supplied campus is
/// ignored. An out-of-scope / non-existent target is reported as Not Found (existence not leaked).
/// </summary>
public sealed class GetRoleAssignmentOptionsQueryHandler
    : IRequestHandler<GetRoleAssignmentOptionsQuery, RoleAssignmentOptionsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRoleAssignmentOptionsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<RoleAssignmentOptionsDto> Handle(
        GetRoleAssignmentOptionsQuery request, CancellationToken cancellationToken)
    {
        // Only a Staff Leader may use this flow (BR: "Chỉ STAFF/LEADER được sử dụng flow này").
        var isStaffLeader = _currentUser.RoleCode == RoleCodes.Staff
            && _currentUser.SubRole == UserSubRoles.Leader;
        if (!isStaffLeader)
            throw new ForbiddenException("Chỉ Trưởng phòng IC được sử dụng chức năng chỉnh sửa vai trò này.");

        var actorCampusId = _currentUser.PrimaryCampusId
            ?? throw new ForbiddenException("Tài khoản của bạn chưa được gán cơ sở nên không thể quản lý tài khoản.");

        // Target must exist and belong to the caller's campus, and must not be the caller.
        var target = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == request.TargetUserId)
            .Select(u => new { u.UserId, u.PrimaryCampusId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Account", request.TargetUserId);

        if (_currentUser.UserId is not null && target.UserId == _currentUser.UserId.Value)
            throw new ForbiddenException("Bạn không thể thay đổi vai trò của chính mình.");
        if (target.PrimaryCampusId != actorCampusId)
            throw new NotFoundException("Account", request.TargetUserId);

        var campusName = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == actorCampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var ic = await _db.Departments.AsNoTracking()
            .Where(d => d.CampusId == actorCampusId
                        && d.DepartmentType == "IC"
                        && d.Status == EntityStatuses.Active)
            .Select(d => new IcDepartmentOptionDto { DepartmentId = d.DepartmentId, Name = d.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var generals = await _db.Departments.AsNoTracking()
            .Where(d => d.CampusId == actorCampusId
                        && d.DepartmentType == "GENERAL"
                        && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.Name)
            .Select(d => new { d.DepartmentId, d.Name, d.HeadUserId })
            .ToListAsync(cancellationToken);

        var generalOptions = generals.Select(d =>
        {
            var hasHead = d.HeadUserId != null;
            var isCurrentTargetHead = d.HeadUserId == request.TargetUserId;
            return new GeneralDepartmentOptionDto
            {
                DepartmentId = d.DepartmentId,
                Name = d.Name,
                HasHead = hasHead,
                IsCurrentTargetHead = isCurrentTargetHead,
                Selectable = !hasHead || isCurrentTargetHead,
            };
        }).ToList();

        // ── Successor picker data. The role change refuses to leave a department headless, so when
        //    the target holds a seat the modal has to offer a replacement in the same step; the
        //    write side re-validates every candidate it is given. ──
        var headed = generals.FirstOrDefault(d => d.HeadUserId == request.TargetUserId);
        HeadedDepartmentDto? headedDepartment = null;
        if (headed is not null)
        {
            var candidates = await _db.Users.AsNoTracking()
                .Where(u => u.DepartmentId == headed.DepartmentId
                            && u.UserId != request.TargetUserId
                            && u.Role!.RoleCode == RoleCodes.Department
                            && u.SubRole == UserSubRoles.Staff
                            && u.Status == UserStatuses.Active
                            && u.PrimaryCampusId == actorCampusId)
                .OrderBy(u => u.FullName)
                .Select(u => new HeadReplacementCandidateDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                })
                .ToListAsync(cancellationToken);

            headedDepartment = new HeadedDepartmentDto
            {
                DepartmentId = headed.DepartmentId,
                Name = headed.Name,
                ReplacementCandidates = candidates,
            };
        }

        return new RoleAssignmentOptionsDto
        {
            CampusId = actorCampusId,
            CampusName = campusName,
            IcDepartment = ic,
            GeneralDepartments = generalOptions,
            HeadedDepartment = headedDepartment,
        };
    }
}
