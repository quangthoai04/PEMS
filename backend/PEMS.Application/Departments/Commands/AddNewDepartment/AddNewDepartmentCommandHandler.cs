using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Departments.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Departments.Commands.AddNewDepartment;

/// <summary>
/// UC-101 handler. Creates a GENERAL department in the Staff Leader's own campus with all system
/// fields server-populated (type GENERAL, head NULL, status ACTIVE), after enforcing role/scope,
/// campus active state, name validation and per-campus uniqueness. Writes an audit log.
/// </summary>
public sealed class AddNewDepartmentCommandHandler
    : IRequestHandler<AddNewDepartmentCommand, AddNewDepartmentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public AddNewDepartmentCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<AddNewDepartmentResponse> Handle(
        AddNewDepartmentCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderDepartmentScope.EnsureStaffLeaderCampus(_currentUser);

        var campus = await _db.Campuses
            .FirstOrDefaultAsync(c => c.CampusId == campusId, cancellationToken)
            ?? throw new BusinessRuleException("Cơ sở hiện tại không tồn tại.", DepartmentErrorCodes.CampusNotFound);

        if (campus.Status != EntityStatuses.Active)
            throw new BusinessRuleException(
                "Không thể tạo phòng ban trong cơ sở đã ngừng hoạt động.", DepartmentErrorCodes.CampusInactive);

        var name = Regex.Replace(request.Name?.Trim() ?? string.Empty, @"\s+", " ");

        var lowered = name.ToLower();
        var exists = await _db.Departments.AnyAsync(
            d => d.CampusId == campusId && d.Name.ToLower() == lowered, cancellationToken);
        if (exists)
            throw new ConflictException(
                "Tên phòng ban đã tồn tại trong cơ sở này.", DepartmentErrorCodes.DepartmentNameAlreadyExists);

        var actorId = _currentUser.UserId;
        var now = _clock.UtcNow;

        var department = new Department
        {
            CampusId = campusId,
            Name = name,
            DepartmentType = "GENERAL",
            HeadUserId = null,
            Status = EntityStatuses.Active,
            CreatedAt = now,
            CreatedBy = actorId,
        };

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.Departments.Add(department);
            await _db.SaveChangesAsync(cancellationToken);

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = campusId,
                Action = "CREATE_DEPARTMENT",
                EntityType = "Department",
                EntityId = department.DepartmentId,
                Changes = new List<AuditLogChange>
                {
                    new AuditLogChange
                    {
                        FieldName = "Department",
                        NewValueText = JsonSerializer.Serialize(new { name, campusId, departmentType = "GENERAL", status = EntityStatuses.Active })
                    }
                },
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return new AddNewDepartmentResponse
        {
            DepartmentId = department.DepartmentId,
            CampusId = campusId,
            CampusName = campus.Name,
            Name = department.Name,
            HeadUserId = null,
            HeadFullName = null,
            Status = department.Status,
            DepartmentType = department.DepartmentType,
            CanToggleStatus = true,
            CreatedAt = department.CreatedAt,
            Message = "Đã thêm phòng ban mới.",
        };
    }
}
