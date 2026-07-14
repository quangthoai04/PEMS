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

/// <summary>Role-assignment options for the Staff Leader "Chỉnh sửa vai trò" modal.</summary>
public sealed class RoleAssignmentOptionsDto
{
    public ulong CampusId { get; init; }
    public string CampusName { get; init; } = default!;

    /// <summary>The campus IC department (ACTIVE), or null when the campus has none.</summary>
    public IcDepartmentOptionDto? IcDepartment { get; init; }

    public IReadOnlyList<GeneralDepartmentOptionDto> GeneralDepartments { get; init; }
        = new List<GeneralDepartmentOptionDto>();
}
