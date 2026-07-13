using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Campuses.Common;

/// <summary>
/// Everything the availability rule needs to know about one campus, plus the evaluated
/// <see cref="CampusOperationalReadinessDto"/>. The resolved Staff Leader id is exposed for
/// flows that route work to them (visit-request coordinator) — it is only set when EXACTLY
/// ONE valid leader exists, never picked arbitrarily among several (BR-86-20).
/// </summary>
public sealed class CampusAvailabilitySnapshot
{
    public ulong CampusId { get; init; }
    public string CampusCode { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? City { get; init; }
    public string Status { get; init; } = null!;
    public int ActiveIcDepartmentCount { get; init; }
    public ulong? ActiveIcDepartmentId { get; init; }
    public int ValidStaffLeaderCount { get; init; }
    public ulong? ValidStaffLeaderUserId { get; init; }
    public CampusOperationalReadinessDto Readiness { get; init; } = null!;

    public bool IsAvailableForVisitRegistration => Readiness.IsAvailableForVisitRegistration;
}

/// <summary>
/// Single source of the UC-86 §8 operational-availability data query. Loads the raw facts
/// (campus status, ACTIVE IC departments, valid ACTIVE Staff Leaders) and classifies them via
/// <see cref="CampusReadinessRule"/>. Used by campus list/detail readiness, the registration
/// campus options endpoint, the status-impact preview, the enable response and the visit-request
/// submit recheck — dropdown, badge and backend guard always agree (§9).
///
/// A "valid Staff Leader" is verified from the user rows themselves, never from
/// campuses.ic_head_user_id (§8.5): role_code STAFF + sub_role LEADER + user ACTIVE +
/// primary_campus_id = campus + member of that campus's ACTIVE IC department.
/// </summary>
public static class CampusAvailabilityEvaluator
{
    /// <summary>Evaluates one campus. Returns null when the campus does not exist.</summary>
    public static async Task<CampusAvailabilitySnapshot?> EvaluateAsync(
        IApplicationDbContext db, ulong campusId, CancellationToken ct)
    {
        var map = await EvaluateAsync(db, new[] { campusId }, ct);
        return map.TryGetValue(campusId, out var snapshot) ? snapshot : null;
    }

    /// <summary>
    /// Batch evaluation keyed by campus id. Campuses that do not exist are absent from the
    /// result. Three simple queries + in-memory aggregation (Pomelo-safe, no GROUP JOIN SQL).
    /// </summary>
    public static async Task<Dictionary<ulong, CampusAvailabilitySnapshot>> EvaluateAsync(
        IApplicationDbContext db, IReadOnlyCollection<ulong> campusIds, CancellationToken ct)
    {
        var result = new Dictionary<ulong, CampusAvailabilitySnapshot>();
        if (campusIds.Count == 0)
            return result;

        var ids = campusIds.Distinct().ToList();

        var campuses = await db.Campuses.AsNoTracking()
            .Where(c => ids.Contains(c.CampusId))
            .Select(c => new { c.CampusId, c.CampusCode, c.Name, c.City, c.Status, c.IcHeadUserId })
            .ToListAsync(ct);

        var icDepartments = await db.Departments.AsNoTracking()
            .Where(d => ids.Contains(d.CampusId)
                        && d.DepartmentType == "IC"
                        && d.Status == EntityStatuses.Active)
            .Select(d => new { d.CampusId, d.DepartmentId })
            .ToListAsync(ct);

        // Valid leaders (§8.3): STAFF + LEADER + ACTIVE, primary campus in scope, and member of
        // an ACTIVE IC department OF THAT SAME campus (department mismatch disqualifies).
        var leaders = await db.Users.AsNoTracking()
            .Where(u => u.Role!.RoleCode == RoleCodes.Staff
                        && u.SubRole == UserSubRoles.Leader
                        && u.Status == UserStatuses.Active
                        && u.PrimaryCampusId.HasValue
                        && ids.Contains(u.PrimaryCampusId.Value)
                        && u.Department != null
                        && u.Department.DepartmentType == "IC"
                        && u.Department.Status == EntityStatuses.Active
                        && u.Department.CampusId == u.PrimaryCampusId.Value)
            .Select(u => new { u.UserId, CampusId = u.PrimaryCampusId!.Value })
            .ToListAsync(ct);

        var icByCampus = icDepartments.ToLookup(d => d.CampusId);
        var leadersByCampus = leaders.ToLookup(u => u.CampusId);

        foreach (var campus in campuses)
        {
            var campusIcDepts = icByCampus[campus.CampusId].ToList();
            var campusLeaders = leadersByCampus[campus.CampusId].ToList();

            var singleLeaderId = campusLeaders.Count == 1 ? campusLeaders[0].UserId : (ulong?)null;
            var icHeadConsistent = singleLeaderId is null
                || campus.IcHeadUserId is null
                || campus.IcHeadUserId == singleLeaderId;

            var readiness = CampusReadinessRule.Evaluate(
                campus.Status,
                campusIcDepts.Count,
                campusLeaders.Count,
                icHeadConsistent);

            result[campus.CampusId] = new CampusAvailabilitySnapshot
            {
                CampusId = campus.CampusId,
                CampusCode = campus.CampusCode,
                Name = campus.Name,
                City = campus.City,
                Status = campus.Status,
                ActiveIcDepartmentCount = campusIcDepts.Count,
                ActiveIcDepartmentId = campusIcDepts.Count == 1 ? campusIcDepts[0].DepartmentId : null,
                ValidStaffLeaderCount = campusLeaders.Count,
                ValidStaffLeaderUserId = singleLeaderId,
                Readiness = readiness,
            };
        }

        return result;
    }
}
