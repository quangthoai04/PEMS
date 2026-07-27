namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>
/// Stable, machine-readable error codes for the Department Leader personnel-management slice
/// (see 27_07_26_PEMS_DEPARTMENT_LEADER_PERSONNEL_MANAGEMENT master spec §18). Surfaced through the
/// controlled exceptions so the frontend maps them to localized messages instead of parsing prose.
/// Keep in sync with the frontend map in
/// <c>features/department-leader-personnel/api/departmentLeaderError.ts</c>.
/// </summary>
public static class DepartmentLeaderErrorCodes
{
    // ── Actor / department scope ──────────────────────────────────────────────

    /// <summary>Caller is not a DEPARTMENT + LEADER account (JWT shape or DB re-read). → 403.</summary>
    public const string DepartmentLeaderRequired = "DEPARTMENT_LEADER_REQUIRED";

    /// <summary>The Leader has no <c>department_id</c> / <c>primary_campus_id</c>. → 422.</summary>
    public const string DepartmentContextMissing = "DEPARTMENT_CONTEXT_MISSING";

    /// <summary>The department (or its campus) is not ACTIVE. → 422.</summary>
    public const string DepartmentNotActive = "DEPARTMENT_NOT_ACTIVE";

    /// <summary>
    /// The caller carries DEPARTMENT+LEADER claims but is no longer the department's
    /// <c>head_user_id</c>, or the department is not a GENERAL one. → 403.
    /// </summary>
    public const string DepartmentScopeForbidden = "DEPARTMENT_SCOPE_FORBIDDEN";

    // ── Target personnel ──────────────────────────────────────────────────────

    /// <summary>
    /// The target account does not exist OR is outside the caller's department. Deliberately the SAME
    /// code/status for both so a Leader cannot probe which user ids exist in other departments
    /// (spec §11 — one convention, used consistently). → 404.
    /// </summary>
    public const string PersonnelNotFound = "PERSONNEL_NOT_FOUND";

    /// <summary>
    /// The target exists inside the department but the requested operation is not permitted on it
    /// (e.g. the Leader acting on their own account through a staff-only path). → 403.
    /// </summary>
    public const string PersonnelScopeForbidden = "PERSONNEL_SCOPE_FORBIDDEN";

    /// <summary>Requested a status transition that is not ACTIVE↔INACTIVE. → 422.</summary>
    public const string PersonnelInvalidStatus = "PERSONNEL_INVALID_STATUS";

    /// <summary>A Leader tried to disable their own account. → 422.</summary>
    public const string PersonnelSelfDisableForbidden = "PERSONNEL_SELF_DISABLE_FORBIDDEN";

    /// <summary>Tried to disable the department's current head. → 422.</summary>
    public const string CurrentLeaderDisableForbidden = "CURRENT_LEADER_DISABLE_FORBIDDEN";

    /// <summary>The target still holds unfinished responsibilities, so it cannot be disabled. → 409.</summary>
    public const string PersonnelHasActiveResponsibilities = "PERSONNEL_HAS_ACTIVE_RESPONSIBILITIES";

    /// <summary>
    /// Tried to activate a PENDING_EMAIL_CONFIRMATION account through the status toggle — activation
    /// happens only by confirming the email. → 422.
    /// </summary>
    public const string PersonnelEmailConfirmationPending = "PERSONNEL_EMAIL_CONFIRMATION_PENDING";

    /// <summary>Tried to activate a LOCKED account — that needs the dedicated security flow. → 422.</summary>
    public const string PersonnelSecurityLocked = "PERSONNEL_SECURITY_LOCKED";

    // ── Identity / email ──────────────────────────────────────────────────────

    /// <summary>The submitted email equals the current one after normalization (informational). → 422.</summary>
    public const string EmailUnchanged = "EMAIL_UNCHANGED";

    /// <summary>The submitted email is structurally invalid or uses a disallowed domain. → 400.</summary>
    public const string InvalidEmail = "INVALID_EMAIL";

    /// <summary>Another <c>users</c> row already owns this email. → 409.</summary>
    public const string AccountEmailAlreadyExists = "ACCOUNT_EMAIL_ALREADY_EXISTS";

    /// <summary>Another account's auth-provider identity already owns this email. → 409.</summary>
    public const string AuthIdentityConflict = "AUTH_IDENTITY_CONFLICT";

    // ── Leadership transfer ───────────────────────────────────────────────────

    /// <summary>The chosen successor is not a usable candidate (missing / wrong role / self). → 422.</summary>
    public const string LeaderCandidateInvalid = "LEADER_CANDIDATE_INVALID";

    /// <summary>The chosen successor is not ACTIVE. → 422.</summary>
    public const string LeaderCandidateNotActive = "LEADER_CANDIDATE_NOT_ACTIVE";

    /// <summary>The chosen successor belongs to another department or campus. → 422.</summary>
    public const string LeaderCandidateWrongDepartment = "LEADER_CANDIDATE_WRONG_DEPARTMENT";

    /// <summary>
    /// Under the row lock the department's head is no longer the caller — a concurrent transfer won.
    /// The loser reloads instead of overwriting. → 409.
    /// </summary>
    public const string LeadershipAlreadyChanged = "LEADERSHIP_ALREADY_CHANGED";

    /// <summary>The transfer could not be applied atomically (state moved under the lock). → 409.</summary>
    public const string LeadershipTransferConflict = "LEADERSHIP_TRANSFER_CONFLICT";

    // ── Confirmation resend ───────────────────────────────────────────────────

    /// <summary>Resend requested inside the per-account cooldown. → 422.</summary>
    public const string ResendTooSoon = "RESEND_TOO_SOON";

    /// <summary>The account reached its maximum number of confirmation resends. → 422.</summary>
    public const string ResendLimitReached = "RESEND_LIMIT_REACHED";

    /// <summary>Resend requested for an account that is not PENDING_EMAIL_CONFIRMATION. → 422.</summary>
    public const string PersonnelNotPending = "PERSONNEL_NOT_PENDING";
}
