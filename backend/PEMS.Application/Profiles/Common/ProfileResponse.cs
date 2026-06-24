namespace PEMS.Application.Profiles.Common;

/// <summary>
/// Self-service profile projection shared by UC-14 (View) and UC-15 (Update).
/// Mirrors the ViewProfileResponse contract in docs/ProfileManagement/01_UC14_VIEW_PROFILE_SPEC.md §10.
/// Deliberately omits every secret (password_hash, tokens, OTP, security columns).
/// </summary>
public sealed class ProfileResponse
{
    public ulong UserId { get; init; }

    public string FullName { get; init; } = default!;
    public string? AvatarUrl { get; init; }

    /// <summary>"MALE" | "FEMALE" | "OTHER" | null — already normalized to the UI contract.</summary>
    public string? Gender { get; init; }

    public string Email { get; init; } = default!;
    public string? Phone { get; init; }
    public string? Nationality { get; init; }

    public string RoleCode { get; init; } = default!;

    /// <summary>"LEADER" | "STAFF" | null.</summary>
    public string? SubRole { get; init; }

    /// <summary>Role label shown on the UI. Currently the raw role code (FE shows it directly).</summary>
    public string DisplayRole { get; init; } = default!;

    /// <summary>"Trưởng phòng" (LEADER) | "Nhân viên" (STAFF) | null. Only meaningful for STAFF/DEPARTMENT.</summary>
    public string? DisplayPosition { get; init; }

    public string? StudentCode { get; init; }

    public ProfileCampusInfo? Campus { get; init; }

    /// <summary>
    /// "Cơ sở" label. Null for VISITOR (no campus). For internal users it is the campus
    /// name, or "Chưa cấu hình" when the campus cannot be resolved.
    /// </summary>
    public string? DisplayCampusName { get; init; }

    public ProfileDepartmentInfo? Department { get; init; }
    public string? DisplayDepartmentName { get; init; }

    public string Status { get; init; } = default!;
}

public sealed class ProfileCampusInfo
{
    public ulong CampusId { get; init; }
    public string CampusCode { get; init; } = default!;
    public string Name { get; init; } = default!;
}

public sealed class ProfileDepartmentInfo
{
    public ulong DepartmentId { get; init; }
    public string Name { get; init; } = default!;
    public string DepartmentType { get; init; } = default!;
}
