using PEMS.Domain.Constants;

namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>
/// The per-row capability flags the list/detail responses carry (spec §17). They exist so the UI stops
/// deciding permissions from <c>localStorage</c> — but they are a RENDERING hint only: every command
/// re-derives the same rules server-side, so hand-crafting a request that ignores a false flag still
/// fails (spec §5.3).
/// </summary>
public static class DepartmentPersonnelActionFlags
{
    /// <summary>
    /// Everyone inside the department can be opened. Scope itself is the gate; a row that reached the
    /// caller is by construction in their department.
    /// </summary>
    public static bool CanView() => true;

    /// <summary>
    /// Profile/identity is editable for every member of the department, in any account status —
    /// including LOCKED, whose email must stay correctable (spec §12.1/§12.9). The Leader may also fix
    /// their own record.
    /// </summary>
    public static bool CanEdit() => true;

    /// <summary>
    /// Disable is offered only for an ACTIVE member who is neither the caller nor the seated head.
    /// A true flag is not a promise: the status-impact check may still find active responsibilities.
    /// </summary>
    public static bool CanDisable(ulong targetUserId, string targetStatus, ulong actorUserId, ulong? departmentHeadUserId)
        => targetStatus == UserStatuses.Active
           && targetUserId != actorUserId
           && targetUserId != departmentHeadUserId;

    /// <summary>
    /// Enable is offered only from INACTIVE. PENDING activates by confirming its email and LOCKED needs
    /// the security flow, so neither is offered here (spec §15).
    /// </summary>
    public static bool CanEnable(ulong targetUserId, string targetStatus, ulong actorUserId)
        => targetStatus == UserStatuses.Inactive && targetUserId != actorUserId;

    /// <summary>
    /// A leadership successor must be an ACTIVE DEPARTMENT/STAFF of this department — never the caller
    /// and never the seated head (spec §16).
    /// </summary>
    public static bool CanTransferLeadershipTo(
        ulong targetUserId, string targetStatus, string? targetSubRole, ulong actorUserId, ulong? departmentHeadUserId)
        => targetStatus == UserStatuses.Active
           && targetSubRole == UserSubRoles.Staff
           && targetUserId != actorUserId
           && targetUserId != departmentHeadUserId;

    /// <summary>Resending a confirmation link only makes sense while the account is still pending.</summary>
    public static bool CanResendEmailConfirmation(string targetStatus)
        => targetStatus == UserStatuses.PendingEmailConfirmation;

    /// <summary>Display title derived from the sub-role — never stored, never sent by the client.</summary>
    public static string ResolvePosition(string? subRole)
        => subRole == UserSubRoles.Leader ? "Trưởng phòng" : "Nhân viên";
}
