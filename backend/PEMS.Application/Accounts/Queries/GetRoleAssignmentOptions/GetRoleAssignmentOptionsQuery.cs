using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Accounts.Queries.GetRoleAssignmentOptions;

/// <summary>
/// Returns the campus-scoped role-assignment options for the Staff Leader "Chỉnh sửa vai trò"
/// flow (UC-100-SL). Campus is taken from the authenticated Staff Leader — never from the client.
/// The <paramref name="TargetUserId"/> lets the backend mark the department the target account is
/// currently head of as selectable (so a Department Leader can keep their own department).
/// </summary>
public sealed class GetRoleAssignmentOptionsQuery : IRequest<RoleAssignmentOptionsDto>
{
    /// <summary>The account whose role is being edited.</summary>
    public ulong TargetUserId { get; set; }
}

/// <summary>One IC department option (auto-assigned for role STAFF).</summary>
public sealed class IcDepartmentOptionDto
{
    public ulong DepartmentId { get; init; }
    public string Name { get; init; } = default!;
}

/// <summary>One active GENERAL department option for the Department-Leader dropdown.</summary>
public sealed class GeneralDepartmentOptionDto
{
    public ulong DepartmentId { get; init; }
    public string Name { get; init; } = default!;

    /// <summary><c>head_user_id IS NOT NULL</c>.</summary>
    public bool HasHead { get; init; }

    /// <summary>The current head of this department is the target account being edited.</summary>
    public bool IsCurrentTargetHead { get; init; }

    /// <summary><c>!HasHead || IsCurrentTargetHead</c> — whether the dropdown may select it.</summary>
    public bool Selectable { get; init; }
}

/// <summary>
/// The department the target account currently heads, plus the colleagues who could take it over.
/// Present only when the target is somebody's <c>head_user_id</c> — the modal uses it to ask for a
/// successor in the same step as the role change (spec §8.6), instead of refusing the change and
/// sending the user to a reassign flow that would leave the account unmanageable.
/// </summary>
public sealed class HeadedDepartmentDto
{
    public ulong DepartmentId { get; init; }
    public string Name { get; init; } = default!;

    /// <summary>Active DEPARTMENT/STAFF members of this department. Empty means no valid successor.</summary>
    public IReadOnlyList<HeadReplacementCandidateDto> ReplacementCandidates { get; init; }
        = new List<HeadReplacementCandidateDto>();
}

/// <summary>One account eligible to become the new head of <see cref="HeadedDepartmentDto"/>.</summary>
public sealed class HeadReplacementCandidateDto
{
    public ulong UserId { get; init; }
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;
}

/// <summary>Role-assignment options for the Staff Leader "Chỉnh sửa vai trò" modal.</summary>
public sealed class RoleAssignmentOptionsDto
{
    public ulong CampusId { get; init; }
    public string CampusName { get; init; } = default!;

    /// <summary>The campus IC department (ACTIVE), or null when the campus has none.</summary>
    public IcDepartmentOptionDto? IcDepartment { get; init; }

    public IReadOnlyList<GeneralDepartmentOptionDto> GeneralDepartments { get; init; }
        = new List<GeneralDepartmentOptionDto>();

    /// <summary>Null unless the target account currently heads a GENERAL department.</summary>
    public HeadedDepartmentDto? HeadedDepartment { get; init; }
}
