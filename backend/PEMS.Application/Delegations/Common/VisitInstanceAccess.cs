using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// Resolves the calling user's relation to a campus instance — the single rule used by every
/// visit-process read/write handler so the meaning of "HOST / STAFF_LEADER / HO / VISITOR_OWNER /
/// IC_SUPPORT / DEPT_SUPPORT / STUDENT / NONE" never drifts between endpoints.
/// </summary>
public static class VisitInstanceAccess
{
    public const string Host = "HOST";
    public const string StaffLeader = "STAFF_LEADER";
    public const string Ho = "HO";
    public const string VisitorOwner = "VISITOR_OWNER";
    public const string IcSupport = "IC_SUPPORT";
    public const string DeptSupport = "DEPT_SUPPORT";
    public const string Student = "STUDENT";
    public const string None = "NONE";

    public static async Task<string> ResolveRelationAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        VisitRequestCampus instance,
        VisitRequest visit,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return None;

        var userId = currentUser.UserId.Value;
        var roleCode = currentUser.RoleCode;
        var subRole = currentUser.SubRole;

        if (instance.CurrentHostUserId == userId)
            return Host;

        if (roleCode == RoleCodes.Staff
            && string.Equals(subRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
            && currentUser.PrimaryCampusId == instance.CampusId)
            return StaffLeader;

        if (roleCode == RoleCodes.Ho)
            return Ho;

        if (roleCode == RoleCodes.Visitor && visit.VisitorUserId == userId)
            return VisitorOwner;

        // Supporting participant (only an ACCEPTED, non-host row grants a relation).
        var acceptedRole = await db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                        && p.UserId == userId
                        && p.Status == ParticipantStatuses.Accepted
                        && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        return acceptedRole switch
        {
            ParticipantRoles.IcSupport => IcSupport,
            ParticipantRoles.DeptSupport => DeptSupport,
            ParticipantRoles.Student => Student,
            _ => None,
        };
    }

    /// <summary>True for relations allowed to view the internal participant list / process data
    /// (everyone with an internal relation; the guest/visitor owner is intentionally excluded).</summary>
    public static bool CanViewInternal(string relation)
        => relation is Host or StaffLeader or Ho or IcSupport or DeptSupport or Student;
}
