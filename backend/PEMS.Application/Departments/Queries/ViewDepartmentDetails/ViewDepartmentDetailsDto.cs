namespace PEMS.Application.Departments.Queries.ViewDepartmentDetails;

/// <summary>
/// UC-105 detail projection. General fields only (name/campus/head/status). <c>departmentType</c>
/// + <c>canEditName</c>/<c>canToggleStatus</c> drive UI button visibility; never rendered as a
/// "Loại phòng ban" field. No sensitive head fields are exposed.
/// </summary>
public sealed class ViewDepartmentDetailsDto
{
    public ulong DepartmentId { get; init; }
    public string Name { get; init; } = default!;
    public ulong CampusId { get; init; }
    public string? CampusCode { get; init; }
    public string CampusName { get; init; } = default!;
    public ulong? HeadUserId { get; init; }
    public string? HeadFullName { get; init; }
    public string Status { get; init; } = default!;
    public string DepartmentType { get; init; } = default!;

    /// <summary>True only for GENERAL departments (IC is not editable by a Staff Leader).</summary>
    public bool CanEditName { get; init; }

    /// <summary>True only for GENERAL departments (IC has no status toggle).</summary>
    public bool CanToggleStatus { get; init; }
}
