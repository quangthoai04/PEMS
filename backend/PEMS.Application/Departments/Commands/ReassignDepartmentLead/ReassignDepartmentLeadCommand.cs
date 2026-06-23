using MediatR;

namespace PEMS.Application.Departments.Commands.ReassignDepartmentLead;

public class ReassignDepartmentLeadCommand : IRequest<ReassignDepartmentLeadResponse>
{
    public ulong DepartmentId { get; set; }
    public ulong NewLeaderUserId { get; set; }
}
