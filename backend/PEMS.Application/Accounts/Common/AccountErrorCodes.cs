namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Stable, machine-readable error codes for the Account Management UCs (UC-95..UC-100).
/// Surfaced via <see cref="PEMS.Application.Common.Exceptions.AuthBusinessException"/> so the
/// frontend can map them to localized messages. Keep in sync with the frontend
/// account-management error map.
/// </summary>
public static class AccountErrorCodes
{
    /// <summary>Authenticated but lacks the Account-List/Search permission. → 403.</summary>
    public const string AccountListForbidden = "ACCOUNT_LIST_FORBIDDEN";

    /// <summary>Tried to read accounts of a campus outside the caller's scope. → 403.</summary>
    public const string CampusScopeForbidden = "CAMPUS_SCOPE_FORBIDDEN";

    /// <summary>sortBy was not in the allowed whitelist. → 400.</summary>
    public const string UnsupportedSortColumn = "UNSUPPORTED_SORT_COLUMN";

    /// <summary>A filter value was structurally invalid. → 400.</summary>
    public const string InvalidAccountFilter = "INVALID_ACCOUNT_FILTER";

    // ── UC-96 Create Staff Leader (Trưởng phòng IC) — see HO_CREATE_STAFF_LEADER spec §8. ──

    /// <summary>The chosen campus does not exist. → 404.</summary>
    public const string CampusNotFound = "CAMPUS_NOT_FOUND";

    /// <summary>The chosen campus is not ACTIVE (C8). → 422.</summary>
    public const string CampusInactive = "CAMPUS_INACTIVE";

    /// <summary>The campus has no ACTIVE IC department (C7). → 422.</summary>
    public const string IcDepartmentMissing = "IC_DEPARTMENT_MISSING";

    /// <summary>The campus has more than one ACTIVE IC department (C6). → 409.</summary>
    public const string IcDepartmentMultiple = "IC_DEPARTMENT_MULTIPLE";

    /// <summary>An email identical to the request already exists (C9). → 409.</summary>
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";

    /// <summary>The campus/IC department already has an ACTIVE Staff Leader (C2). → 409.</summary>
    public const string StaffLeaderAlreadyExistsActive = "STAFF_LEADER_ALREADY_EXISTS_ACTIVE";

    /// <summary>The campus/IC department already has an INACTIVE Staff Leader (C3). → 409.</summary>
    public const string StaffLeaderExistsInactive = "STAFF_LEADER_EXISTS_INACTIVE";

    /// <summary>The campus/IC department already has a LOCKED Staff Leader (C4). → 409.</summary>
    public const string StaffLeaderExistsLocked = "STAFF_LEADER_EXISTS_LOCKED";

    /// <summary>campus.ic_head_user_id and department.head_user_id point to different users (C5). → 409.</summary>
    public const string IcLeaderReferenceMismatch = "IC_LEADER_REFERENCE_MISMATCH";

    /// <summary>Exactly one of campus.ic_head_user_id / department.head_user_id is set (§10.2). → 409.</summary>
    public const string IcLeaderPartialReference = "IC_LEADER_PARTIAL_REFERENCE";

    /// <summary>A STAFF/LEADER exists on the campus+IC dept but is not linked as head (§10.3). → 409.</summary>
    public const string UnlinkedStaffLeaderExists = "UNLINKED_STAFF_LEADER_EXISTS";

    // ── UC-96 Create HO account — see HO_CREATE_HO_ACCOUNT_FULL_CASES spec §6. ──

    /// <summary>The campus already has an ACTIVE HO (Case B). → 409.</summary>
    public const string CampusHoAlreadyActive = "CAMPUS_HO_ALREADY_ACTIVE";

    /// <summary>The campus already has an INACTIVE HO (Case C). → 409.</summary>
    public const string CampusHoInactiveExists = "CAMPUS_HO_INACTIVE_EXISTS";

    /// <summary>The campus already has a LOCKED HO (Case D). → 409.</summary>
    public const string CampusHoLockedExists = "CAMPUS_HO_LOCKED_EXISTS";

    /// <summary>The campus has more than one HO record — inconsistent data (Case E). → 409.</summary>
    public const string CampusHoDataInconsistent = "CAMPUS_HO_DATA_INCONSISTENT";

    // ── UC-97 Manage HO status — see HO_CREATE_HO_ACCOUNT spec §7. ──

    /// <summary>HO status can't be toggled in the generic endpoint; needs a dedicated flow. → 403.</summary>
    public const string HoStatusChangeRequiresSpecialFlow = "HO_STATUS_CHANGE_REQUIRES_SPECIAL_FLOW";

    // ── Replace Staff Leader — see REPLACE_STAFF_LEADER spec §15. ──

    /// <summary>Replace requested but the campus has no Staff Leader yet — use Create instead. → 409.</summary>
    public const string CampusHasNoStaffLeader = "CAMPUS_HAS_NO_STAFF_LEADER";

    /// <summary>The chosen replacement user is not a valid IC Staff for this campus. → 422.</summary>
    public const string InvalidReplacementCandidate = "INVALID_REPLACEMENT_CANDIDATE";

    // ── Safe account role change (active responsibilities) — see
    //    27_07_26_PEMS_SAFE_ACCOUNT_ROLE_CHANGE_ACTIVE_RESPONSIBILITY spec §11. ──

    /// <summary>
    /// The target account (or the requested new role) is outside the Staff Leader's managed set:
    /// only STAFF/STAFF, DEPARTMENT/LEADER and STUDENT may be role-changed here (spec §3). → 403.
    /// </summary>
    public const string AccountRoleTargetNotManageable = "ACCOUNT_ROLE_TARGET_NOT_MANAGEABLE";

    /// <summary>
    /// The account still holds at least one active responsibility (host / coordinator / participant /
    /// logistics / department head), so its role cannot change yet (spec §8). Carries the blocker
    /// breakdown under <c>data</c>. → 409.
    /// </summary>
    public const string AccountRoleChangeBlockedByActiveResponsibilities =
        "ACCOUNT_ROLE_CHANGE_BLOCKED_BY_ACTIVE_RESPONSIBILITIES";

    /// <summary>
    /// The replacement department head supplied with a role change is not usable — not a member of
    /// the department being handed over, not active, or supplied when no handover is needed
    /// (spec §8.6). → 422.
    /// </summary>
    public const string InvalidDepartmentHeadReplacement = "INVALID_DEPARTMENT_HEAD_REPLACEMENT";

    // ── ADMIN = Global Read + Security Control (ADMIN_ACCOUNT_MANAGEMENT spec §31). ADMIN no longer
    //    performs personnel/account business management; those belong to HO / Staff Leader. Each
    //    refusal carries its own code so the client can tell WHICH boundary it hit. ──

    /// <summary>ADMIN tried to create an account — personnel provisioning belongs to HO/SL. → 403.</summary>
    public const string AdminAccountCreationNotAllowed = "ADMIN_ACCOUNT_CREATION_NOT_ALLOWED";

    /// <summary>ADMIN tried to edit role / campus / department / identity of an account. → 403.</summary>
    public const string AdminAccountEditNotAllowed = "ADMIN_ACCOUNT_EDIT_NOT_ALLOWED";

    /// <summary>
    /// ADMIN tried to move an account between the BUSINESS states (ACTIVE ↔ INACTIVE). That is
    /// personnel management, not security control. → 422.
    /// </summary>
    public const string AdminBusinessStatusChangeNotAllowed = "ADMIN_BUSINESS_STATUS_CHANGE_NOT_ALLOWED";

    /// <summary>Security lock requested from a state other than ACTIVE (spec §4). → 422.</summary>
    public const string SecurityLockInvalidState = "SECURITY_LOCK_INVALID_STATE";

    /// <summary>Security unlock requested for an account that is not LOCKED (spec §4). → 422.</summary>
    public const string SecurityUnlockInvalidState = "SECURITY_UNLOCK_INVALID_STATE";

    /// <summary>A security lock/unlock arrived without the mandatory reason (spec §17/§19). → 422.</summary>
    public const string SecurityReasonRequired = "SECURITY_REASON_REQUIRED";

    /// <summary>ADMIN tried to security-lock or unlock their own account (spec §20). → 403.</summary>
    public const string AccountSecuritySelfActionForbidden = "ACCOUNT_SECURITY_SELF_ACTION_FORBIDDEN";

    /// <summary>
    /// A non-ADMIN caller (HO / Staff Leader) tried to lock or unlock a LOCKED account. LOCKED is a
    /// SECURITY state owned by ADMIN, not a business status (spec §39). → 422.
    /// </summary>
    public const string SecurityStatusRequiresAdmin = "SECURITY_STATUS_REQUIRES_ADMIN";

    /// <summary>The caller's role may not manage account status at all (fail-closed, spec §16.4). → 403.</summary>
    public const string AccountStatusChangeForbidden = "ACCOUNT_STATUS_CHANGE_FORBIDDEN";

    // ── Related Visitor Accounts tab (Staff Leader) — see UC_StaffLeader_Related_Visitor_Accounts_Tab. ──

    /// <summary>Caller is not an active Staff Leader (STAFF/LEADER + campus) for this tab. → 403.</summary>
    public const string RelatedVisitorForbidden = "RELATED_VISITOR_FORBIDDEN";

    /// <summary>A Staff Leader tried to view a Visitor not related to their campus. → 403/404.</summary>
    public const string VisitorScopeForbidden = "VISITOR_SCOPE_FORBIDDEN";
}
