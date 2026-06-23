using MediatR;

namespace PEMS.Application.Departments.Commands.RemovePersonnel;

public class RemovePersonnelCommand : IRequest<RemovePersonnelResponse>
{
    public ulong DepartmentId { get; set; }
    public ulong UserId { get; set; }
}
