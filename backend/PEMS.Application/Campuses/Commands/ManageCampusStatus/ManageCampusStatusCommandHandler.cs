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
/// Disable (BR-86-07/08/09): blocked with 409 while the campus still has visit instances in a
/// non-terminal status — checked on <c>visit_request_campuses.campus_id</c> inside the same
/// transaction that flips the status, so a submit that races the preview is still caught
/// (BR-86-17). CLOSED/CANCELLED/REJECTED never block; nothing is cascaded, cancelled or deleted
/// (BR-86-11/12/13).
///
/// Enable (BR-86-14/15): requires complete master data + an ACTIVE IC department; a Staff Leader
/// is NOT required — it only gates operational availability, which is recomputed for the
/// response. Idempotent no-op on same-status requests. Audits ENABLE_CAMPUS/DISABLE_CAMPUS.
///
/// A successful DISABLE also revokes every active session of the campus's internal
/// HO/ADMIN/STAFF/DEPARTMENT/STUDENT accounts (users.status is never touched — an
/// org-level lock, mirror of UC-106 department disable) and writes one aggregate security
/// policy event with a CAMPUS_DISABLED_SESSIONS_REVOKED detail marker. VISITOR and users of
/// other campuses are never affected. Enable never restores revoked
/// sessions (BR-AUTH-CAMPUS-09).
/// </summary>
public sealed class ManageCampusStatusCommandHandler
    : IRequestHandler<ManageCampusStatusCommand, ManageCampusStatusResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoleAccessPolicy _accessPolicy;
    private readonly IDateTimeService _clock;
    private readonly ISessionService _sessionService;
    private readonly ISecurityAuditService _securityAudit;

    public ManageCampusStatusCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRoleAccessPolicy accessPolicy,
        IDateTimeService clock,
        ISessionService sessionService,
        ISecurityAuditService securityAudit)
    {
        _db = db;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _sessionService = sessionService;
        _securityAudit = securityAudit;
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

        // Idempotent no-op: already at the requested status (UC-86 §17) — no updated_at
        // change, no audit row.
        if (string.Equals(previousStatus, newStatus, StringComparison.Ordinal))
        {
            var currentReadiness = await CampusAvailabilityEvaluator.EvaluateAsync(
                _db, campus.CampusId, cancellationToken);
            return new ManageCampusStatusResponse
            {
                CampusId = campus.CampusId,
                Status = campus.Status,
                UpdatedAt = campus.UpdatedAt ?? campus.CreatedAt,
                UpdatedBy = campus.UpdatedBy,
                Message = "Campus đã ở trạng thái này.",
                Readiness = currentReadiness?.Readiness,
            };
        }

        // ── Enable (INACTIVE → ACTIVE): master data + active IC dept (BR-86-14) ──
        if (newStatus == EntityStatuses.Active)
            await EnsureActivationAllowedAsync(campus, cancellationToken);

        var actorId = _currentUser.UserId;
        var now = _clock.VietnamNow;
        var action = newStatus == EntityStatuses.Active ? "ENABLE_CAMPUS" : "DISABLE_CAMPUS";

        var affectedUserIds = new List<ulong>();
        var revokedSessionCount = 0;

        // ── Transactional transition: the disable-blocker recheck, the status write and the
        // session revocation must be one unit (§23.2) — a visit submit committed after the
        // preview still blocks here, and status + revocation commit or roll back together.
        // ISessionService shares this scoped DbContext, so its writes join this transaction. ──
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            if (newStatus == EntityStatuses.Inactive)
            {
                var impact = await CampusStatusImpactCalculator.ComputeDisableImpactAsync(
                    _db, campus.CampusId, cancellationToken);

                if (impact.HasBlockers)
                {
                    // No update, no audit (§24: never audit a change that did not happen).
                    throw new ConflictException(
                        "Không thể ngừng hoạt động campus vì còn chuyến thăm chưa hoàn tất.",
                        CampusErrorCodes.CampusHasActiveVisits,
                        new
                        {
                            total = impact.BlockerCount,
                            byStatus = impact.BlockersByStatus,
                        });
                }

                // Campus-scoped internal accounts (HO/ADMIN/STAFF/DEPARTMENT/STUDENT) of this
                // campus lose access: revoke their active sessions. users.status is NOT touched;
                // VISITOR and users of other campuses are never affected.
                affectedUserIds = await _db.Users.AsNoTracking()
                    .Where(u => u.PrimaryCampusId == campus.CampusId
                                && (u.Role!.RoleCode == RoleCodes.Ho
                                    || u.Role!.RoleCode == RoleCodes.Admin
                                    || u.Role!.RoleCode == RoleCodes.Staff
                                    || u.Role!.RoleCode == RoleCodes.Department
                                    || u.Role!.RoleCode == RoleCodes.Student))
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken);
            }

            campus.Status = newStatus;
            campus.UpdatedAt = now;
            campus.UpdatedBy = actorId;

            foreach (var userId in affectedUserIds)
                revokedSessionCount += await _sessionService.RevokeAllActiveSessionsAsync(
                    userId, SessionRevokeReasons.CampusDisabled, actorId, cancellationToken);

            // One aggregate security event per disable (doc §17) — inside the transaction so a
            // failed disable never leaves a success event behind. No per-session events, no
            // tokens/PII in the detail text.
            if (newStatus == EntityStatuses.Inactive)
            {
                await _securityAudit.WriteSecurityEventAsync(
                    userId: actorId,
                    emailSnapshot: _currentUser.Email,
                    eventType: SecurityEventTypes.SecurityPolicyCheck,
                    result: "SUCCESS",
                    detailText: $"event={SecurityEventDetailMarkers.CampusDisabledSessionsRevoked}; campusId={campus.CampusId}; affectedUserCount={affectedUserIds.Count}; revokedSessionCount={revokedSessionCount}",
                    cancellationToken: cancellationToken);
            }

            var changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "Status",
                    OldValueText = previousStatus,
                    NewValueText = JsonSerializer.Serialize(new { status = newStatus, reason = request.Reason })
                }
            };
            if (newStatus == EntityStatuses.Inactive)
            {
                changes.Add(new AuditLogChange { FieldName = "AffectedAccountCount", NewValueText = affectedUserIds.Count.ToString() });
                changes.Add(new AuditLogChange { FieldName = "RevokedSessionCount", NewValueText = revokedSessionCount.ToString() });
            }

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = campus.CampusId,
                Action = action,
                EntityType = "Campus",
                EntityId = campus.CampusId,
                Changes = changes,
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

        // Recompute operational readiness for the response (§19.2 step 10) — e.g. an enable
        // without a Staff Leader returns ACTIVE + isAvailableForVisitRegistration = false.
        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(
            _db, campus.CampusId, cancellationToken);

        return new ManageCampusStatusResponse
        {
            CampusId = campus.CampusId,
            Status = campus.Status,
            UpdatedAt = now,
            UpdatedBy = actorId,
            Message = newStatus == EntityStatuses.Active
                ? "Đã kích hoạt campus."
                : affectedUserIds.Count > 0
                    ? $"Đã ngừng hoạt động campus. {affectedUserIds.Count} tài khoản không còn quyền truy cập hệ thống."
                    : "Đã ngừng hoạt động campus.",
            Readiness = snapshot?.Readiness,
            AffectedAccountCount = affectedUserIds.Count,
            RevokedSessionCount = revokedSessionCount,
        };
    }

    /// <summary>
    /// BR-86-14: a campus may only be re-activated when its required master data is complete
    /// and it has at least one ACTIVE IC department. A Staff Leader is NOT required (BR-86-15).
    /// </summary>
    private async Task EnsureActivationAllowedAsync(
        Domain.Entities.Campuses.Campus campus, CancellationToken ct)
    {
        var missing = CampusActivationRequirements.GetMissingMasterData(campus);
        if (missing.Count > 0)
        {
            throw new BusinessRuleException(
                "Không thể kích hoạt campus vì còn thiếu thông tin bắt buộc: " + string.Join(", ", missing) + ".",
                CampusErrorCodes.CampusActivationMasterDataIncomplete);
        }

        var hasActiveIcDept = await _db.Departments.AsNoTracking().AnyAsync(
            d => d.CampusId == campus.CampusId
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
}
