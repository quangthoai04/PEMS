using MediatR;

namespace PEMS.Application.Departments.Commands.AddNewDepartment;

/// <summary>
/// UC-101 Add New Department (Staff Leader). Creates a GENERAL department in the caller's own
/// campus. Only the name is accepted — campus, type, head, status and audit fields are all
/// server-populated; any other field sent is ignored.
/// </summary>
public sealed class AddNewDepartmentCommand : IRequest<AddNewDepartmentResponse>
{
    public string Name { get; set; } = string.Empty;
}
