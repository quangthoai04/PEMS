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
    /// True when an HO caller may edit this account's basic info (full name / email) — see
    /// HO_BASIC_INFO spec §11. Drives the "Chỉnh sửa thông tin" button in the detail modal.
    /// </summary>
    public bool CanEditBasicInfo { get; init; }

    /// <summary>Reason the basic-info edit is disabled for an HO caller (null when allowed).</summary>
    public string? EditBasicInfoDisabledReason { get; init; }
}
