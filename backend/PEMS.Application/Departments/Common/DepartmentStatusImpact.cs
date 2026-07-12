using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Departments.Common;

/// <summary>One non-terminal business dependency preventing a department from being disabled.</summary>
public sealed class DepartmentStatusBlocker
{
    public string Type { get; init; } = default!;
    public int Count { get; init; }
    public string Message { get; init; } = default!;
}

/// <summary>Computed impact of disabling a department (affected accounts, sessions, blockers).</summary>
public sealed class DepartmentStatusImpact
{
    public IReadOnlyList<ulong> AffectedUserIds { get; init; } = Array.Empty<ulong>();
    public int AffectedAccountCount => AffectedUserIds.Count;
    public int ActiveSessionCount { get; init; }
    public IReadOnlyList<DepartmentStatusBlocker> Blockers { get; init; } = Array.Empty<DepartmentStatusBlocker>();
    public bool HasBlockers => Blockers.Count > 0;
}

/// <summary>
/// UC-106 shared impact/blocker computation, used by both the status-impact preview query and
/// the manage-status command so the confirmation modal and the backend guard can never disagree.
///
/// Affected accounts = users with <c>role_code = DEPARTMENT</c> and <c>sub_role LEADER/STAFF</c>
/// linked to the department (never derived from <c>head_user_id</c>; other roles are excluded
/// even if abnormal data gives them the same department_id).
///
/// Hard blockers use the real schema only: <c>visit_logistics_items.requested_to_department_id</c>
/// rows whose status is non-terminal (per <see cref="LogisticsItemStatus"/>).
/// </summary>
public static class DepartmentStatusImpactCalculator
{
    /// <summary>Logistics statuses that still require the department to act (non-terminal).</summary>
    private static readonly string[] OpenLogisticsStatuses =
    {
        LogisticsItemStatus.Requested,
        LogisticsItemStatus.ChangeProposed,
        LogisticsItemStatus.Assigned,
        LogisticsItemStatus.Accepted,
        LogisticsItemStatus.InProgress,
    };

    public const string OpenLogisticsBlockerType = "OPEN_LOGISTICS_ITEMS";

    public static async Task<DepartmentStatusImpact> ComputeDisableImpactAsync(
        IApplicationDbContext db, ulong departmentId, DateTime now, CancellationToken cancellationToken)
    {
        var affectedUserIds = await db.Users
            .Where(u => u.DepartmentId == departmentId
                        && u.Role!.RoleCode == RoleCodes.Department
                        && (u.SubRole == UserSubRoles.Leader || u.SubRole == UserSubRoles.Staff))
            .Select(u => u.UserId)
            .ToListAsync(cancellationToken);

        var activeSessionCount = affectedUserIds.Count == 0
            ? 0
            : await db.UserSessions.CountAsync(
                s => affectedUserIds.Contains(s.UserId) && s.RevokedAt == null && s.ExpiresAt > now,
                cancellationToken);

        var blockers = new List<DepartmentStatusBlocker>();

        var openLogisticsCount = await db.VisitLogisticsItems.CountAsync(
            i => i.RequestedToDepartmentId == departmentId && OpenLogisticsStatuses.Contains(i.Status),
            cancellationToken);

        if (openLogisticsCount > 0)
        {
            blockers.Add(new DepartmentStatusBlocker
            {
                Type = OpenLogisticsBlockerType,
                Count = openLogisticsCount,
                Message = $"Còn {openLogisticsCount} yêu cầu hậu cần chưa hoàn tất được giao cho phòng ban.",
            });
        }

        return new DepartmentStatusImpact
        {
            AffectedUserIds = affectedUserIds,
            ActiveSessionCount = activeSessionCount,
            Blockers = blockers,
        };
    }
}
