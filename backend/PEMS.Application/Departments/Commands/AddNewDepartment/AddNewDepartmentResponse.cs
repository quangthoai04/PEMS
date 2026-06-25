using System;

namespace PEMS.Application.Departments.Commands.AddNewDepartment;

/// <summary>Created department projection returned by UC-101 (mirrors a list row + a message).</summary>
public sealed class AddNewDepartmentResponse
{
    public ulong DepartmentId { get; init; }
    public ulong CampusId { get; init; }
    public string CampusName { get; init; } = default!;
    public string Name { get; init; } = default!;
    public ulong? HeadUserId { get; init; }
    public string? HeadFullName { get; init; }
    public string Status { get; init; } = "ACTIVE";
    public string DepartmentType { get; init; } = "GENERAL";
    public bool CanToggleStatus { get; init; } = true;
    public DateTime CreatedAt { get; init; }
    public string Message { get; init; } = "Đã thêm phòng ban mới.";
}
