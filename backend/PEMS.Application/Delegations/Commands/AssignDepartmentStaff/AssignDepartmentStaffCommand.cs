using MediatR;

namespace PEMS.Application.Delegations.Commands.AssignDepartmentStaff;

public class AssignDepartmentStaffCommand : IRequest<ulong>
{
    public ulong ParticipantId { get; set; }
    public ulong DepartmentStaffUserId { get; set; }
    public string Note { get; set; }

    public AssignDepartmentStaffCommand(ulong participantId, ulong departmentStaffUserId, string note)
    {
        ParticipantId = participantId;
        DepartmentStaffUserId = departmentStaffUserId;
        Note = note;
    }
}
