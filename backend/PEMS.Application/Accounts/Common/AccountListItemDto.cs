namespace PEMS.Application.Accounts.Common;

/// <summary>
/// One row in the Account List (UC-95) / Search &amp; Filter (UC-99) result.
/// Deliberately omits all sensitive columns (password_hash, provider_subject,
/// tokens, security stamps). Profile fields included here are the same ones the
/// admin UI already displays.
/// </summary>
public sealed class AccountListItemDto
{
    public ulong UserId { get; init; }
    public string Email { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public string? Phone { get; init; }
    public PEMS.Domain.Enums.Gender? Gender { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Nationality { get; init; }
    public string? StudentCode { get; init; }

    public string RoleCode { get; init; } = default!;
    public string RoleName { get; init; } = default!;
    public string? SubRole { get; init; }

    public ulong? CampusId { get; init; }
    public string? CampusCode { get; init; }
    public string? CampusName { get; init; }

    public ulong? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }

    public string Status { get; init; } = default!;
    public string? CreatedVia { get; init; }

    public IReadOnlyList<string> Providers { get; init; } = Array.Empty<string>();

    public DateTime? LastLoginAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    /// <summary>True when the caller may open this row's details (UC-98).</summary>
    public bool CanViewDetails { get; init; }

    /// <summary>True when the caller may change this row's role (UC-100).</summary>
    public bool CanUpdateRole { get; init; }

    /// <summary>True when the caller may lock/unlock this row (UC-97).</summary>
    public bool CanManageStatus { get; init; }

    /// <summary>
    /// When <see cref="CanManageStatus"/> is false, the machine-readable reason the status toggle
    /// is hidden (e.g. SELF_ACCOUNT, HO_STATUS_CHANGE_REQUIRES_SPECIAL_FLOW, OTHER_CAMPUS_HO).
    /// Null when the toggle is available. See HO_CREATE_HO_ACCOUNT spec §12.3.
    /// </summary>
    public string? HideStatusToggleReason { get; init; }

    /// <summary>True when this row is the caller's own account (UI hides the status toggle).</summary>
    public bool IsCurrentUser { get; init; }

    /// <summary>
    /// True when an HO caller may edit this row's basic info (full name / email). See
    /// HO_BASIC_INFO spec §11: caller HO, target not self, not LOCKED, and target is HO or
    /// STAFF/LEADER. Not driven by CanUpdateRole (HO cannot change roles).
    /// </summary>
    public bool CanEditBasicInfo { get; init; }

    /// <summary>
    /// When <see cref="CanEditBasicInfo"/> is false for an HO caller, the machine-readable reason
    /// (SELF_ACCOUNT | ACCOUNT_LOCKED | TARGET_ROLE_NOT_MANAGEABLE | NO_PERMISSION). Null otherwise.
    /// </summary>
    public string? EditBasicInfoDisabledReason { get; init; }

    /// <summary>
    /// True when the caller may SECURITY-lock this row (ACTIVE → LOCKED). ADMIN only, target ACTIVE,
    /// never the caller's own row (ADMIN_ACCOUNT_MANAGEMENT spec §23).
    ///
    /// <para>
    /// Deliberately separate from <see cref="CanManageStatus"/>, which keeps its original meaning —
    /// the business ACTIVE ↔ INACTIVE toggle owned by HO / Staff Leader. Overloading one flag with
    /// both meanings is what would let an ADMIN's security lock render as a personnel toggle (and an
    /// HO's toggle render as a lock) on any screen that only reads the flag.
    /// </para>
    /// </summary>
    public bool CanSecurityLock { get; init; }

    /// <summary>
    /// True when the caller may SECURITY-unlock this row (LOCKED → ACTIVE). ADMIN only, target
    /// LOCKED, never the caller's own row.
    /// </summary>
    public bool CanSecurityUnlock { get; init; }

    /// <summary>
    /// For an ADMIN caller with neither security action available, why — SELF_ACCOUNT |
    /// ACCOUNT_INACTIVE | ACCOUNT_PENDING_EMAIL_CONFIRMATION | NO_PERMISSION. Null when an action is
    /// offered, and null for HO / Staff Leader (for whom security actions are simply not part of the
    /// screen, which is not a "disabled reason" to explain).
    /// </summary>
    public string? SecurityActionDisabledReason { get; init; }
}
