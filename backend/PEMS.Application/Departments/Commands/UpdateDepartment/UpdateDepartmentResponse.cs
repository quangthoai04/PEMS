using System;

namespace PEMS.Application.Departments.Commands.UpdateDepartment;

/// <summary>UC-102 result. <c>changed=false</c> means the trimmed name equalled the current one
/// (no DB write, no audit — §9.2/AF-07).</summary>
public sealed class UpdateDepartmentResponse
{
    public ulong DepartmentId { get; init; }
    public string Name { get; init; } = default!;
    public string CampusName { get; init; } = default!;
    public string? HeadFullName { get; init; }
    public string Status { get; init; } = default!;
    public string DepartmentType { get; init; } = default!;
    public DateTime? UpdatedAt { get; init; }
    public bool Changed { get; init; }
    public string Message { get; init; } = default!;
}
