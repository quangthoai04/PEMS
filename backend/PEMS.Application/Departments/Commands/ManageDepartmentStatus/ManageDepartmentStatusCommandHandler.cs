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
///
/// UC-106 (2026-07-12 baseline): active DEPARTMENT accounts are NOT a blocker anymore. Disabling
/// revokes every active session of the department's DEPARTMENT+LEADER/STAFF users (users.status is
/// never touched) inside the same transaction as the status change + audit. Disable is still
/// blocked by non-terminal business dependencies: open logistics requests, plus participant
/// dependencies of the department's users — pending DEPT_SUPPORT invitations and
/// accepted/assigned participations on operational visits (BR-UC106-24..39). Enabling requires
/// the campus to be ACTIVE, never restores old sessions, and never runs the participant check.
/// </summary>
public sealed class ManageDepartmentStatusCommandHandler
    : IRequestHandler<ManageDepartmentStatusCommand, ManageDepartmentStatusResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly ISessionService _sessionService;

    public ManageDepartmentStatusCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        ISessionService sessionService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _sessionService = sessionService;
    }

    public async Task<ManageDepartmentStatusResponse> Handle(
        ManageDepartmentStatusCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderDepartmentScope.EnsureStaffLeaderCampus(_currentUser);
        var newStatus = request.Status.Trim().ToUpperInvariant();

        var department = await _db.Departments
            .Include(d => d.Campus)
            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, cancellationToken)
            ?? throw new NotFoundException("Department", request.DepartmentId);

        if (department.CampusId != campusId)
            throw new NotFoundException("Department", request.DepartmentId);

        if (department.DepartmentType != "GENERAL")
            throw new AuthBusinessException(
                DepartmentErrorCodes.DepartmentTypeNotManageable,
                "Không thể thay đổi trạng thái của phòng ban mặc định (IC).", 403);

        // No-op: same status → no updated_at/updated_by change, no audit, no session revoke.
        if (department.Status == newStatus)
            return new ManageDepartmentStatusResponse
            {
                DepartmentId = department.DepartmentId,
                Name = department.Name,
                OldStatus = department.Status,
                NewStatus = department.Status,
                Status = department.Status,
                Changed = false,
                UpdatedAt = department.UpdatedAt,
                Message = "Trạng thái phòng ban không thay đổi.",
            };

        return newStatus == EntityStatuses.Active
            ? await EnableAsync(department, cancellationToken)
            : await DisableAsync(department, campusId, cancellationToken);
    }

    private async Task<ManageDepartmentStatusResponse> DisableAsync(
        Domain.Entities.Departments.Department department, ulong campusId, CancellationToken cancellationToken)
    {
        var now = _clock.VietnamNow;

        var impact = await DepartmentStatusImpactCalculator.ComputeDisableImpactAsync(
            _db, department.DepartmentId, now, cancellationToken);

        // Non-terminal business dependencies (open logistics + participant invitations/
        // participations) still block; nothing is changed or revoked (BR-UC106-35).
        if (impact.HasBlockers)
            throw new ConflictException(
                "Không thể ngừng hoạt động phòng ban vì còn lời mời, lượt tham gia hoặc nghiệp vụ chưa hoàn tất.",
                DepartmentErrorCodes.DepartmentStatusBlockedByDependencies,
                new { blockers = impact.Blockers });

        var oldStatus = department.Status;

        // Status change + session revocation + audit must commit or roll back together
        // (BR-UC106-19). ISessionService shares this scoped DbContext, so its SaveChanges
        // calls participate in this transaction (same pattern as ReplaceStaffLeader).
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);

        department.Status = EntityStatuses.Inactive;
        department.UpdatedAt = now;
        department.UpdatedBy = _currentUser.UserId;

        // Revoke every active session of the department's DEPARTMENT+LEADER/STAFF users.
        // users.status is deliberately NOT touched (BR-UC106-08).
        var revokedSessionCount = 0;
        foreach (var userId in impact.AffectedUserIds)
            revokedSessionCount += await _sessionService.RevokeAllActiveSessionsAsync(
                userId, SessionRevokeReasons.DepartmentDisabled, _currentUser.UserId, cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = campusId,
            Action = "MANAGE_DEPARTMENT_STATUS",
            EntityType = "Department",
            EntityId = department.DepartmentId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange { FieldName = "Status", OldValueText = oldStatus, NewValueText = EntityStatuses.Inactive },
                new AuditLogChange { FieldName = "AffectedAccountCount", NewValueText = impact.AffectedAccountCount.ToString() },
                new AuditLogChange { FieldName = "RevokedSessionCount", NewValueText = revokedSessionCount.ToString() },
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ManageDepartmentStatusResponse
        {
            DepartmentId = department.DepartmentId,
            Name = department.Name,
            OldStatus = oldStatus,
            NewStatus = department.Status,
            Status = department.Status,
            Changed = true,
            AffectedAccountCount = impact.AffectedAccountCount,
            RevokedSessionCount = revokedSessionCount,
            UpdatedAt = department.UpdatedAt,
            Message = impact.AffectedAccountCount > 0
                ? $"Đã ngừng hoạt động phòng ban. {impact.AffectedAccountCount} tài khoản không còn quyền truy cập hệ thống."
                : "Đã ngừng hoạt động phòng ban.",
        };
    }

    private async Task<ManageDepartmentStatusResponse> EnableAsync(
        Domain.Entities.Departments.Department department, CancellationToken cancellationToken)
    {
        // BR-UC106-14: a department can only be re-enabled inside an ACTIVE campus.
        if (department.Campus is null || department.Campus.Status != EntityStatuses.Active)
            throw new BusinessRuleException(
                "Không thể kích hoạt phòng ban trong cơ sở đã ngừng hoạt động.",
                DepartmentErrorCodes.CampusInactive);

        var oldStatus = department.Status;
        var now = _clock.VietnamNow;

        department.Status = EntityStatuses.Active;
        department.UpdatedAt = now;
        department.UpdatedBy = _currentUser.UserId;

        // Enable never touches users.status and never restores revoked sessions
        // (BR-UC106-15/16): only users with users.status = ACTIVE can log in again.
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = department.CampusId,
            Action = "MANAGE_DEPARTMENT_STATUS",
            EntityType = "Department",
            EntityId = department.DepartmentId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange { FieldName = "Status", OldValueText = oldStatus, NewValueText = EntityStatuses.Active },
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new ManageDepartmentStatusResponse
        {
            DepartmentId = department.DepartmentId,
            Name = department.Name,
            OldStatus = oldStatus,
            NewStatus = department.Status,
            Status = department.Status,
            Changed = true,
            UpdatedAt = department.UpdatedAt,
            Message = "Đã kích hoạt lại phòng ban. Các tài khoản đang hoạt động có thể đăng nhập lại.",
        };
    }
}
