using MediatR;
using PEMS.Domain.Enums;

namespace PEMS.Application.Departments.Commands.AddDepartmentPersonnel;

public class AddDepartmentPersonnelCommand : IRequest<AddDepartmentPersonnelResponse>
{
    public ulong DepartmentId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public Gender Gender { get; set; }
    public string Role { get; set; } = "Nhân viên";
}
