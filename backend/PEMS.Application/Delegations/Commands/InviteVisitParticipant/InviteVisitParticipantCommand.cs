using MediatR;
using PEMS.Application.Emails.Preview;

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
    /// <summary>
    /// The message the Host edited and approved in the FINAL preview, or null to send the template.
    ///
    /// <para>
    /// Carries a signed token, so "the mail that goes out is the mail they approved" is checked rather
    /// than assumed. The system action block — the real accept/decline tokens — is still injected by the
    /// backend either way; an author may not place one.
    /// </para>
    /// </summary>
    ApprovedEmailContent? ApprovedContent = null) : IRequest<InviteVisitParticipantResponse>;

public static class InviteParticipantTypes
{
    public const string IcSupport = "IC_SUPPORT";
    public const string Student = "STUDENT";
    public const string DeptSupport = "DEPT_SUPPORT";
}
