using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Accounts.Queries.GetCampusDepartments;

/// <summary>
/// Returns the active GENERAL departments of the caller's own campus, used to populate the
/// "Department Leader" department dropdown in the Create Account / Update Role modals
/// (UC-96-SL / UC-100-SL). Campus is taken from the caller — never from the client.
/// </summary>
public sealed class GetCampusDepartmentsQuery : IRequest<IReadOnlyList<CampusDepartmentDto>>
{
}

public sealed class CampusDepartmentDto
{
    public ulong DepartmentId { get; init; }
    public string Name { get; init; } = default!;
    public bool HasHead { get; init; }
}
