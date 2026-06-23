using MediatR;
using PEMS.Domain.Enums;

namespace PEMS.Application.Departments.Commands.UpdateDepartmentPersonnel;

public class UpdateDepartmentPersonnelCommand : IRequest<UpdateDepartmentPersonnelResponse>
{
    public ulong DepartmentId { get; set; }
    public ulong UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public Gender Gender { get; set; }
}

public class UpdateDepartmentPersonnelResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
