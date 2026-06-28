using MediatR;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Delegations.Commands.AssignDepartmentStaff;

public class AssignDepartmentStaffCommand : IRequest<ulong>
{
    public ulong ParticipantId { get; set; }
    public ulong DepartmentStaffUserId { get; set; }
    public string Note { get; set; }
    public EmailOverride? EmailOverride { get; set; }

    public AssignDepartmentStaffCommand(ulong participantId, ulong departmentStaffUserId, string note, EmailOverride? emailOverride = null)
    {
        ParticipantId = participantId;
        DepartmentStaffUserId = departmentStaffUserId;
        Note = note;
        EmailOverride = emailOverride;
    }
}
