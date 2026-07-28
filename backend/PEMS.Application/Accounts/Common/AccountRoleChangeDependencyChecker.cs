using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Loads every responsibility the target account still holds and hands them to
/// <see cref="AccountRoleChangeDependencyRule"/> for classification. Read-only by construction: a
/// blocked role change must leave the database exactly as it found it (spec §5/§21).
///
/// Queries pre-filter by user id only; the status matrix lives entirely in the rule so the
/// classification cannot drift between production and tests.
/// </summary>
public static class AccountRoleChangeDependencyChecker
{
    /// <summary>
    /// Runs the six blocker groups for <paramref name="targetUserId"/>. The department-head group is
    /// evaluated only when the target is leaving the Department-Leader role or moving to another
    /// department (spec §8.6) — staying Leader of the same department is not a handover.
    /// </summary>
    /// <remarks>
    /// The requested new shape arrives as primitives rather than
    /// <c>AccountProvisioningRules.ResolvedShape</c>: that type is internal, and keeping this entry
    /// point free of it lets the unit suite drive the checker directly.
    /// </remarks>
    public static async Task<AccountRoleChangeImpact> CheckAsync(
        IApplicationDbContext db,
        ulong targetUserId,
        string oldRoleCode,
        string? oldSubRole,
        ulong? oldDepartmentId,
        string newRoleCode,
        string? newSubRole,
        ulong? newDepartmentId,
        CancellationToken cancellationToken)
    {
        var hostCandidates = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.CurrentHostUserId == targetUserId)
            .Select(c => new HostAssignmentCandidate
            {
                VisitInstanceId = c.VisitInstanceId,
                VisitStatus = c.Status,
            })
            .ToListAsync(cancellationToken);

        var coordinatorCandidates = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.CoordinatorUserId == targetUserId)
            .Select(c => new CoordinatorAssignmentCandidate
            {
                VisitInstanceId = c.VisitInstanceId,
                VisitStatus = c.Status,
            })
            .ToListAsync(cancellationToken);

        // Visits this user has handed down to one of their department staff and where that staff
        // member is still on the hook. Same shape the reception progress list uses to decide whom to
        // show as "Người phụ trách", so a role change and that screen agree on who owns the duty.
        var delegatedInstanceIds = await db.VisitParticipants.AsNoTracking()
            .Where(p => p.AssignedBy == targetUserId
                        && p.UserId != targetUserId
                        && p.ParticipantRole == ParticipantRoles.DeptSupport
                        && (p.Status == ParticipantStatuses.Assigned
                            || p.Status == ParticipantStatuses.Accepted))
            .Select(p => p.VisitInstanceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // The instance's own current_host_user_id travels with the row so the rule can tell the
        // canonical Host participant from an orphaned IC_HOST anomaly without a second round-trip.
        var participantCandidates = await db.VisitParticipants.AsNoTracking()
            .Where(p => p.UserId == targetUserId)
            .Select(p => new ParticipantResponsibilityCandidate
            {
                VisitInstanceId = p.VisitInstanceId,
                ParticipantRole = p.ParticipantRole,
                ParticipantStatus = p.Status,
                VisitStatus = p.VisitInstance.Status,
                InstanceHostUserId = p.VisitInstance.CurrentHostUserId,
                DelegatedToSubstitute = delegatedInstanceIds.Contains(p.VisitInstanceId),
            })
            .ToListAsync(cancellationToken);

        var logisticsCandidates = await db.VisitLogisticsItems.AsNoTracking()
            .Where(l => l.AssignedToUserId == targetUserId || l.ReceivedBy == targetUserId)
            .Select(l => new LogisticsResponsibilityCandidate
            {
                LogisticsItemId = l.LogisticsItemId,
                VisitInstanceId = l.VisitInstanceId,
                LogisticsStatus = l.Status,
                VisitStatus = l.VisitInstance.Status,
                AssignedToUserId = l.AssignedToUserId,
                ReceivedBy = l.ReceivedBy,
            })
            .ToListAsync(cancellationToken);

        var departmentHead = await ResolveDepartmentHeadBlockerAsync(
            db, targetUserId, oldRoleCode, oldSubRole, oldDepartmentId,
            newRoleCode, newSubRole, newDepartmentId, cancellationToken);

        return AccountRoleChangeDependencyRule.BuildImpact(
            targetUserId, hostCandidates, coordinatorCandidates,
            participantCandidates, logisticsCandidates, departmentHead);
    }

    private static async Task<DepartmentHeadCandidate?> ResolveDepartmentHeadBlockerAsync(
        IApplicationDbContext db,
        ulong targetUserId,
        string oldRoleCode,
        string? oldSubRole,
        ulong? oldDepartmentId,
        string newRoleCode,
        string? newSubRole,
        ulong? newDepartmentId,
        CancellationToken cancellationToken)
    {
        var wasDepartmentLeader = oldRoleCode == RoleCodes.Department && oldSubRole == UserSubRoles.Leader;
        if (!wasDepartmentLeader || oldDepartmentId is null) return null;

        var staysLeaderOfSameDepartment =
            newRoleCode == RoleCodes.Department
            && newSubRole == UserSubRoles.Leader
            && newDepartmentId == oldDepartmentId;
        if (staysLeaderOfSameDepartment) return null;

        // head_user_id is never cleared here: /departments/reassign-lead is the only sanctioned way
        // to hand the department over, and it must run first (spec §8.6/§15).
        return await db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == oldDepartmentId && d.HeadUserId == targetUserId)
            .Select(d => new DepartmentHeadCandidate
            {
                DepartmentId = d.DepartmentId,
                DepartmentName = d.Name,
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
