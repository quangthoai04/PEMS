using PEMS.Domain.Enums;

namespace PEMS.Application.Accounts.Queries.ViewAccountDetails;

/// <summary>
/// UC-98 detail projection. Deliberately omits every authentication secret
/// (password_hash, provider subject/tokens, reset/otp codes, session tokens).
/// </summary>
public sealed class ViewAccountDetailsDto
{
    public ulong UserId { get; init; }
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string? Phone { get; init; }
    public Gender? Gender { get; init; }
    public string? Nationality { get; init; }

    public string RoleCode { get; init; } = default!;
    public string RoleName { get; init; } = default!;
    public string? SubRole { get; init; }

    /// <summary>Localized role label for the UI (e.g. "Staff").</summary>
    public string DisplayRole { get; init; } = default!;

    /// <summary>Localized position label for the UI (e.g. "Trưởng phòng" for a Staff Leader).</summary>
    public string? DisplayPosition { get; init; }

    public ulong? CampusId { get; init; }
    public string? CampusName { get; init; }
    public ulong? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }

    /// <summary>Student code (MSSV). Only meaningful for STUDENT accounts; null otherwise.</summary>
    public string? StudentCode { get; init; }

    public string Status { get; init; } = default!;
    public string? CreatedVia { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }

    /// <summary>
    /// Which sign-in methods are linked (LOCAL_PASSWORD / GOOGLE_SSO). The provider TYPE only —
    /// never the subject id, the stored tokens or the provider email's credential material. Read-only
    /// context for the ADMIN security review (ADMIN_ACCOUNT_MANAGEMENT spec §8.3).
    /// </summary>
    public IReadOnlyList<string> Providers { get; init; } = Array.Empty<string>();

    // FailedLoginCount / LockedUntil are deliberately NOT projected here (spec §8.4 offers them, it does
    // not require them). Both columns are written by LoginViaCredentialsCommandHandler alone; Google SSO
    // only reads LockedUntil to block a session and never touches either. With password sign-in retired
    // in production they would be a permanently frozen 0 / null on the detail screen — and a counter
    // stuck at 0 reads as "this account has not been attacked" rather than "nothing is counting". The
    // columns stay on the User entity: the lockout still works for any password login that remains, and
    // an ADMIN unlock still clears both (ManageAccountStatusCommandHandler, spec §21 items 22-23).

    /// <summary>
    /// True when an HO caller may edit this account's basic info (full name / email) — see
    /// HO_BASIC_INFO spec §11. Drives the "Chỉnh sửa thông tin" button in the detail modal.
    /// </summary>
    public bool CanEditBasicInfo { get; init; }

    /// <summary>Reason the basic-info edit is disabled for an HO caller (null when allowed).</summary>
    public string? EditBasicInfoDisabledReason { get; init; }

    /// <summary>
    /// True when THIS caller may re-issue the confirmation link for this account — i.e. it is still
    /// PENDING_EMAIL_CONFIRMATION and the caller is authorized to manage it. Drives the "Gửi lại email
    /// xác nhận" button.
    ///
    /// <para>
    /// Computed here rather than left to the client so the button follows one rule instead of a
    /// re-implementation of it: the frontend cannot see sub-roles, campus scope and self-account rules
    /// together as reliably as the query already does. It remains a display hint — every mutation
    /// re-checks the same scope server-side, so a client that ignores this flag gains nothing.
    /// </para>
    /// </summary>
    public bool CanResendEmailConfirmation { get; init; }

    /// <summary>
    /// True when THIS caller may correct a still-pending account's email (which re-issues the
    /// activation link rather than mailing a change notice). Same scope as
    /// <see cref="CanResendEmailConfirmation"/> — both are the "manage a pending account" permission.
    /// </summary>
    public bool CanEditPendingEmail { get; init; }
}
