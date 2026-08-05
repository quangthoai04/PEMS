using MediatR;
using PEMS.Application.Emails.Preview;

namespace PEMS.Application.Delegations.Commands.AssignDepartmentStaff;

public class AssignDepartmentStaffCommand : IRequest<ulong>
{
    public ulong ParticipantId { get; set; }
    public ulong DepartmentStaffUserId { get; set; }
    public string Note { get; set; }

    /// <summary>
    /// The message the Department Leader edited and approved in the FINAL preview, or null to send the
    /// template.
    /// </summary>
    public ApprovedEmailContent? ApprovedContent { get; set; }

    public AssignDepartmentStaffCommand(
        ulong participantId, ulong departmentStaffUserId, string note,
        ApprovedEmailContent? approvedContent = null)
    {
        ParticipantId = participantId;
        DepartmentStaffUserId = departmentStaffUserId;
        Note = note;
        ApprovedContent = approvedContent;
    }
}
