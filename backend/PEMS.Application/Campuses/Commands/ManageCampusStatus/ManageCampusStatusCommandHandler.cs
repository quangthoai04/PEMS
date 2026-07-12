using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Campuses.Commands.ManageCampusStatus;

/// <summary>
/// UC-86 handler. HO toggles a campus between ACTIVE and INACTIVE.
///
/// Mode (per UC-86 §9): this phase uses the SIMPLIFIED disable model — disabling is always
/// allowed (no hard-delete, BR-86-02), existing links are preserved (BR-86-05), and an
/// INACTIVE campus is simply hidden from new business flows (BR-86-04) because
/// <c>GetActiveCampuses</c> only returns ACTIVE rows. Re-activation is guarded by
/// BR-86-06: the campus must have complete master data and at least one ACTIVE IC
/// department. Writes status + updated_by/updated_at and an audit log.
/// </summary>
public sealed class ManageCampusStatusCommandHandler
    : IRequestHandler<ManageCampusStatusCommand, ManageCampusStatusResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoleAccessPolicy _accessPolicy;
    private readonly IDateTimeService _clock;

    public ManageCampusStatusCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRoleAccessPolicy accessPolicy,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _clock = clock;
    }

    public async Task<ManageCampusStatusResponse> Handle(ManageCampusStatusCommand request, CancellationToken cancellationToken)
    {
        // ── Authorization: only HO (and ADMIN superuser) — AF-05 ──
        if (!_accessPolicy.CanAccessCampusManagement(_currentUser))
        {
            throw new AuthBusinessException(
                CampusErrorCodes.CampusManagementForbidden,
                "Bạn không có quyền thay đổi trạng thái campus.", 403);
        }

        var newStatus = (request.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (newStatus != EntityStatuses.Active && newStatus != EntityStatuses.Inactive)
        {
            throw new BusinessRuleException(
                "Trạng thái chỉ có thể là ACTIVE hoặc INACTIVE.", CampusErrorCodes.InvalidCampusStatus);
        }

        var campus = await _db.Campuses
            .FirstOrDefaultAsync(c => c.CampusId == request.CampusId, cancellationToken)
            ?? throw new NotFoundException("Campus", request.CampusId);

        // An HO must not change the status of the campus they belong to (the UI hides the
        // toggle for the own campus; this is the backend gate against a direct API call).
        if (_currentUser.PrimaryCampusId is { } ownCampusId && campus.CampusId == ownCampusId)
            throw new ForbiddenException("Bạn không thể thay đổi trạng thái campus của chính mình.");

        var previousStatus = campus.Status;
        var actorId = _currentUser.UserId;
        var now = _clock.VietnamNow;

        // Idempotent no-op: already at the requested status (UC-86 §10).
        if (string.Equals(previousStatus, newStatus, StringComparison.Ordinal))
        {
            return new ManageCampusStatusResponse
            {
                CampusId = campus.CampusId,
                Status = campus.Status,
                UpdatedAt = campus.UpdatedAt ?? campus.CreatedAt,
                UpdatedBy = campus.UpdatedBy,
                Message = "Campus đã ở trạng thái này.",
            };
        }

        // ── Enable (INACTIVE → ACTIVE): validate required master data + active IC dept (BR-86-06) ──
        if (newStatus == EntityStatuses.Active)
        {
            await EnsureActivationAllowedAsync(campus.CampusId, campus, cancellationToken);
        }

        // ── Disable (ACTIVE → INACTIVE): simplified mode, always allowed (see class summary). ──

        campus.Status = newStatus;
        campus.UpdatedAt = now;
        campus.UpdatedBy = actorId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = campus.CampusId,
            Action = "MANAGE_CAMPUS_STATUS",
            EntityType = "Campus",
            EntityId = campus.CampusId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "Status",
                    OldValueText = previousStatus,
                    NewValueText = JsonSerializer.Serialize(new { status = newStatus, reason = request.Reason })
                }
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new ManageCampusStatusResponse
        {
            CampusId = campus.CampusId,
            Status = campus.Status,
            UpdatedAt = now,
            UpdatedBy = actorId,
            Message = newStatus == EntityStatuses.Active
                ? "Đã kích hoạt campus."
                : "Đã ngừng hoạt động campus.",
        };
    }

    /// <summary>
    /// BR-86-06: a campus may only be re-activated when its required master data is complete
    /// and it has at least one ACTIVE IC department.
    /// </summary>
    private async Task EnsureActivationAllowedAsync(
        ulong campusId, Domain.Entities.Campuses.Campus campus, CancellationToken ct)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(campus.CampusCode)) missing.Add("campusCode");
        if (string.IsNullOrWhiteSpace(campus.Name)) missing.Add("name");
        if (string.IsNullOrWhiteSpace(campus.City)) missing.Add("city");
        if (string.IsNullOrWhiteSpace(campus.Address)) missing.Add("address");
        if (string.IsNullOrWhiteSpace(campus.Phone)) missing.Add("phone");
        if (string.IsNullOrWhiteSpace(campus.Email) || !IsValidEmail(campus.Email)) missing.Add("email");

        if (missing.Count > 0)
        {
            throw new BusinessRuleException(
                "Không thể kích hoạt campus vì còn thiếu thông tin bắt buộc: " + string.Join(", ", missing) + ".",
                CampusErrorCodes.CampusActivationMasterDataIncomplete);
        }

        var hasActiveIcDept = await _db.Departments.AsNoTracking().AnyAsync(
            d => d.CampusId == campusId
                && d.DepartmentType == "IC"
                && d.Status == EntityStatuses.Active,
            ct);

        if (!hasActiveIcDept)
        {
            throw new BusinessRuleException(
                "Không thể kích hoạt campus vì chưa có phòng ban IC đang hoạt động.",
                CampusErrorCodes.CampusActivationMissingIcDepartment);
        }
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var at = email.IndexOf('@');
        var dot = email.LastIndexOf('.');
        return at > 0 && dot > at + 1 && dot < email.Length - 1;
    }
}
