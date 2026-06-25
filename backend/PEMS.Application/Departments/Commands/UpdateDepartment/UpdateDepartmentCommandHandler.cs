using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Departments.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Departments.Commands.UpdateDepartment;

/// <summary>
/// UC-102 handler. Updates ONLY a GENERAL department's name within the Staff Leader's campus.
/// Enforces role/scope (403), existence (404), IC protection (409), per-campus name uniqueness
/// excluding self (409), and the no-op short-circuit when the trimmed name is unchanged
/// (changed=false, no write, no audit — AF-07). Audits field-level name old→new on real change.
/// </summary>
public sealed class UpdateDepartmentCommandHandler
    : IRequestHandler<UpdateDepartmentCommand, UpdateDepartmentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public UpdateDepartmentCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<UpdateDepartmentResponse> Handle(
        UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderDepartmentScope.EnsureStaffLeaderCampus(_currentUser);

        var department = await _db.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, cancellationToken)
            ?? throw new NotFoundException("Department", request.DepartmentId);

        if (department.CampusId != campusId)
            throw new AuthBusinessException(
                DepartmentErrorCodes.DepartmentScopeForbidden,
                "Bạn không có quyền cập nhật phòng ban này.", 403);

        if (department.DepartmentType != "GENERAL")
            throw new ConflictException(
                "Không thể chỉnh sửa phòng Hợp tác quốc tế mặc định.", DepartmentErrorCodes.DepartmentIcNotEditable);

        var newName = Regex.Replace(request.Name?.Trim() ?? string.Empty, @"\s+", " ");

        var campusName = await _db.Campuses.Where(c => c.CampusId == campusId)
            .Select(c => c.Name).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var headFullName = department.HeadUserId == null
            ? null
            : await _db.Users.Where(u => u.UserId == department.HeadUserId)
                .Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);

        // No-op when the trimmed name equals the current name (AF-07): no write, no audit.
        if (string.Equals(department.Name, newName, StringComparison.Ordinal))
        {
            return new UpdateDepartmentResponse
            {
                DepartmentId = department.DepartmentId,
                Name = department.Name,
                CampusName = campusName,
                HeadFullName = headFullName,
                Status = department.Status,
                DepartmentType = department.DepartmentType,
                UpdatedAt = department.UpdatedAt,
                Changed = false,
                Message = "Không có thay đổi nào để cập nhật.",
            };
        }

        var lowered = newName.ToLower();
        var duplicate = await _db.Departments.AnyAsync(
            d => d.CampusId == campusId && d.DepartmentId != department.DepartmentId && d.Name.ToLower() == lowered,
            cancellationToken);
        if (duplicate)
            throw new ConflictException(
                "Tên phòng ban đã tồn tại trong cơ sở này.", DepartmentErrorCodes.DepartmentNameAlreadyExists);

        var oldName = department.Name;
        var now = _clock.UtcNow;
        department.Name = newName;
        department.UpdatedAt = now;
        department.UpdatedBy = _currentUser.UserId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = campusId,
            Action = "UPDATE_DEPARTMENT_NAME",
            EntityType = "Department",
            EntityId = department.DepartmentId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange { FieldName = "name", OldValueText = oldName, NewValueText = newName }
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateDepartmentResponse
        {
            DepartmentId = department.DepartmentId,
            Name = department.Name,
            CampusName = campusName,
            HeadFullName = headFullName,
            Status = department.Status,
            DepartmentType = department.DepartmentType,
            UpdatedAt = department.UpdatedAt,
            Changed = true,
            Message = "Đã cập nhật tên phòng ban.",
        };
    }
}
