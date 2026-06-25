namespace PEMS.Application.Departments.Common;

/// <summary>
/// One row in the Staff Leader Department list (UC-104) / search-filter (UC-103) result.
/// <c>departmentType</c> is returned only so the UI can decide action visibility
/// (<c>canToggleStatus</c>); it must not be rendered as a visible column/filter.
/// </summary>
public sealed class DepartmentListItemDto
{
    public ulong DepartmentId { get; init; }
    public ulong CampusId { get; init; }
    public string CampusName { get; init; } = default!;
    public string Name { get; init; } = default!;
    public ulong? HeadUserId { get; init; }
    public string? HeadFullName { get; init; }
    public string Status { get; init; } = default!;
    public string DepartmentType { get; init; } = default!;

    /// <summary>True only for GENERAL departments; IC (default) departments are not toggleable.</summary>
    public bool CanToggleStatus { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
