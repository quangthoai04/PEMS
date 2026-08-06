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
///  • Create / Edit: ONLY the Host or an accepted (IC/Dept/Student) participant, and only while
///    the visit is live (instance not CLOSED/CANCELLED and request not CANCELLED).
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
        var userId = user.UserId!.Value;
        bool isHost = instance.CurrentHostUserId == userId;
        bool isStaffLeaderOfCampus = user.RoleCode == RoleCodes.Staff
            && string.Equals(user.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
            && user.PrimaryCampusId == instance.CampusId;
        bool isHo = user.RoleCode == RoleCodes.Ho;
        bool isGuestSide = VisitRequestOwnership.IsGuestSide(visit, instance, userId);
        bool isAccepted = acceptedParticipantRole != null;

        bool inScope = isHost || isStaffLeaderOfCampus || isHo || isGuestSide || isAccepted;
        bool isLive = instance.Status != VisitInstanceStatus.Closed
            && instance.Status != VisitInstanceStatus.Cancelled
            && visit.Status != VisitRequestStatuses.Cancelled;
        bool canEdit = (isHost || isAccepted) && isLive;
        return (inScope, canEdit);
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
