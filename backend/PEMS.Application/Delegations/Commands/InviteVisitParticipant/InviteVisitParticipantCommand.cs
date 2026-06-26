using MediatR;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Delegations.Commands.InviteVisitParticipant;

/// <summary>
/// Host invites a supporting participant to a campus instance. The host never picks a Department
/// Leader directly: for DEPT_SUPPORT the host names a GENERAL <see cref="DepartmentId"/> and the
/// backend resolves the department's active leader. Backend re-validates every candidate against the
/// DB — frontend-supplied roles/campus/department are never trusted.
/// </summary>
public sealed record InviteVisitParticipantCommand(
    ulong VisitInstanceId,
    string ParticipantType,   // IC_SUPPORT | STUDENT | DEPT_SUPPORT
    ulong? UserId,            // required for IC_SUPPORT / STUDENT
    ulong? DepartmentId,      // required for DEPT_SUPPORT
    string? Message,
    /// <summary>Optional host-edited subject/body from the "Xem trước email" modal. When
    /// UseEditedContent is true the edited content is used and the system action block (real
    /// accept/decline tokens) is injected by the backend.</summary>
    EmailOverride? EmailOverride = null) : IRequest<InviteVisitParticipantResponse>;

public static class InviteParticipantTypes
{
    public const string IcSupport = "IC_SUPPORT";
    public const string Student = "STUDENT";
    public const string DeptSupport = "DEPT_SUPPORT";
}
