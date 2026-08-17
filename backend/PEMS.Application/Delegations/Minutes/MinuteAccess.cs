using System.Linq;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Minutes;
using PEMS.Shared;

using PEMS.Application.Delegations.Common;
namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// Shared access rules for meeting minutes (UC biên bản):
///  • View: any user in scope of the campus instance (Host / Staff Leader of campus / HO /
///    Visitor owner / accepted non-host participant).
///  • Create / Edit: ONLY the Host or an accepted IC/Student participant, and only while the
///    visit is live (instance not CLOSED/CANCELLED and request not CANCELLED). An accepted
///    DEPARTMENT participant is in scope (may view) but stays read-only here — same split as
///    news creation (spec §6.8): Department contributes logistics, not the visit's own record.
/// Visitor / HO / Staff Leader may never create or edit — they only view.
/// </summary>
internal static class MinuteAccess
{
    public const string StatusDraft = "DRAFT";
    public const string StatusSaved = "SAVED";
    public const int LockMinutes = 10;

    public static (bool InScope, bool CanEdit) Evaluate(
        VisitRequestCampus instance, VisitRequest visit, ICurrentUserService user, string? acceptedParticipantRole)
    {
        // SEC-10/11/Admin-gap: ADMIN is excluded from the whole Visit/Delegation domain
        // (IRoleAccessPolicy.CanAccessVisitManagement), Minutes included. Checked first, before any
        // relationship branch, so a historical Host/accepted-participant row on an account that later
        // became ADMIN can never pass through it.
        if (user.RoleCode == RoleCodes.Admin) return (false, false);

        var userId = user.UserId!.Value;
        bool isHost = instance.CurrentHostUserId == userId;
        bool isStaffLeaderOfCampus = user.RoleCode == RoleCodes.Staff
            && string.Equals(user.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
            && user.PrimaryCampusId == instance.CampusId;
        bool isHo = user.RoleCode == RoleCodes.Ho;
        bool isGuestSide = VisitRequestOwnership.IsGuestSide(visit, instance, userId);
        bool isAccepted = acceptedParticipantRole != null;
        bool isAcceptedDepartment = acceptedParticipantRole == ParticipantRoles.DeptSupport;

        bool inScope = isHost || isStaffLeaderOfCampus || isHo || isGuestSide || isAccepted;
        bool isLive = instance.Status != VisitInstanceStatus.Closed
            && instance.Status != VisitInstanceStatus.Cancelled
            && visit.Status != VisitRequestStatuses.Cancelled;
        bool canEdit = (isHost || (isAccepted && !isAcceptedDepartment)) && isLive;
        return (inScope, canEdit);
    }

    /// <summary>
    /// SEC-10/11: the EF-translatable counterpart of <see cref="Evaluate"/>'s InScope test, for the
    /// list (SearchAndFilterMinutes) and export (PDF/Excel) consumers, which used to apply only a
    /// campus filter — or, for HO, none at all — with no relationship check whatsoever. Must run
    /// BEFORE Count/Skip/Take, never after, so pagination reflects only what the caller may see.
    ///
    /// <para>
    /// Every component reduces to a scalar-field comparison or a nullary-parameter <c>Any()</c>
    /// subquery — both reliably SQL-translatable via Pomelo without needing a pre-built
    /// <c>Expression&lt;Func&lt;&gt;&gt;</c> spliced into the join (which would need LINQKit,
    /// deliberately not added as a dependency here).
    /// </para>
    /// </summary>
    public static IQueryable<Minute> WhereAuthorizedFor(
        IQueryable<Minute> minutes, IApplicationDbContext db, ICurrentUserService user)
    {
        if (!user.IsAuthenticated || user.UserId is not { } uid) return minutes.Where(_ => false);
        if (user.RoleCode == RoleCodes.Admin) return minutes.Where(_ => false); // same principle, same place as Evaluate
        if (user.RoleCode == RoleCodes.Ho) return minutes;

        var isStaffLeader = user.RoleCode == RoleCodes.Staff
            && string.Equals(user.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase);
        var staffLeaderCampusId = isStaffLeader ? user.PrimaryCampusId : null;

        return minutes.Where(m =>
            db.VisitRequestCampuses.Any(vrc => vrc.VisitInstanceId == m.VisitInstanceId
                && (vrc.CurrentHostUserId == uid
                    || (staffLeaderCampusId != null && vrc.CampusId == staffLeaderCampusId)
                    || vrc.VisitRequest.RegistrantUserId == uid
                    || vrc.OperationalContactUserId == uid))
            || db.VisitParticipants.Any(p => p.VisitInstanceId == m.VisitInstanceId
                && p.UserId == uid && p.Status == ParticipantStatuses.Accepted && !p.IsHost));
    }

    /// <summary>True when <paramref name="minute"/> currently has a non-expired edit lock.</summary>
    public static bool IsLockActive(Minute minute, DateTime now)
        => minute.EditLockedBy != null && minute.EditLockExpiresAt.HasValue && minute.EditLockExpiresAt.Value > now;

    /// <summary>
    /// True when <paramref name="userId"/> currently HOLDS an active (non-expired) edit lock on
    /// <paramref name="minute"/>. Edit-mode side actions (sync candidates, user search) must require
    /// this — not just <c>CanEdit</c> — so only the active editor can drive the session.
    /// </summary>
    public static bool IsLockHeldBy(Minute minute, ulong userId, DateTime now)
        => IsLockActive(minute, now) && minute.EditLockedBy == userId;
}
