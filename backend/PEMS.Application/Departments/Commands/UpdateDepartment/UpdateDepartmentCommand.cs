using MediatR;

namespace PEMS.Application.Departments.Commands.UpdateDepartment;

/// <summary>
/// UC-102 Update Department (Staff Leader). ONLY the department name may change — campus, type,
/// head and status are never touched here. The handler ignores everything except Name.
/// </summary>
public sealed class UpdateDepartmentCommand : IRequest<UpdateDepartmentResponse>
{
    public ulong DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
}
