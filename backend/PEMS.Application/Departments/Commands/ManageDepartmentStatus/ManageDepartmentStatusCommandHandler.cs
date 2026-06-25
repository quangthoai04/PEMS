using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Departments.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Departments.Commands.ManageDepartmentStatus;

/// <summary>
/// Enables/disables a GENERAL department in the Staff Leader's own campus. Enforces role/scope,
/// blocks IC departments, restricts to ACTIVE↔INACTIVE, is idempotent on no-op, and audits.
/// </summary>
public sealed class ManageDepartmentStatusCommandHandler
    : IRequestHandler<ManageDepartmentStatusCommand, ManageDepartmentStatusResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public ManageDepartmentStatusCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ManageDepartmentStatusResponse> Handle(
        ManageDepartmentStatusCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderDepartmentScope.EnsureStaffLeaderCampus(_currentUser);
        var newStatus = request.Status.Trim().ToUpperInvariant();

        var department = await _db.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, cancellationToken)
            ?? throw new NotFoundException("Department", request.DepartmentId);

        if (department.CampusId != campusId)
            throw new NotFoundException("Department", request.DepartmentId);

        if (department.DepartmentType != "GENERAL")
            throw new AuthBusinessException(
                DepartmentErrorCodes.DepartmentTypeNotManageable,
                "Không thể thay đổi trạng thái của phòng ban mặc định (IC).", 403);

        if (department.Status == newStatus)
            return new ManageDepartmentStatusResponse
            {
                DepartmentId = department.DepartmentId,
                Status = department.Status,
                Message = "Trạng thái phòng ban không thay đổi.",
            };

        var oldStatus = department.Status;
        var now = _clock.UtcNow;
        department.Status = newStatus;
        department.UpdatedAt = now;
        department.UpdatedBy = _currentUser.UserId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = campusId,
            Action = "MANAGE_DEPARTMENT_STATUS",
            EntityType = "Department",
            EntityId = department.DepartmentId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange { FieldName = "Status", OldValueText = oldStatus, NewValueText = newStatus }
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new ManageDepartmentStatusResponse
        {
            DepartmentId = department.DepartmentId,
            Status = department.Status,
            Message = newStatus == EntityStatuses.Active ? "Đã kích hoạt phòng ban." : "Đã ngừng hoạt động phòng ban.",
        };
    }
}
