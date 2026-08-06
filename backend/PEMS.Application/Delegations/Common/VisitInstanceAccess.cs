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
/// visit-process read/write handler so the meaning of "HOST / STAFF_LEADER / HO / OPERATIONAL_CONTACT /
/// REGISTRANT / IC_SUPPORT / DEPT_SUPPORT / STUDENT / NONE" never drifts between endpoints.
///
/// <para>
/// The single string is for DISPLAY and telemetry. It is a summary of the strongest relation, not an
/// authorization input: a user can be several of these at once (a registrant who also hosts one of
/// their own campuses), and capabilities are unioned by the callers rather than selected here.
/// </para>
/// </summary>
public static class VisitInstanceAccess
{
    public const string Host = "HOST";
    public const string StaffLeader = "STAFF_LEADER";
    public const string Ho = "HO";
    /// <summary>Confirmed operational contact of THIS campus — replaces the old request-wide VISITOR_OWNER.</summary>
    public const string OperationalContact = "OPERATIONAL_CONTACT";
    /// <summary>Submitter of the request. Sees every campus; operates none of them by itself.</summary>
    public const string Registrant = "REGISTRANT";
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

        // Campus-level before request-level: holding THIS campus says more about what the caller may
        // do here than having submitted the request does. Neither is role-gated — a registrant or a
        // contact may be a VISITOR, STAFF or STAFF LEADER account.
        if (VisitRequestOwnership.IsOperationalContact(instance, userId))
            return OperationalContact;

        if (VisitRequestOwnership.IsRegistrant(visit, userId))
            return Registrant;

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
