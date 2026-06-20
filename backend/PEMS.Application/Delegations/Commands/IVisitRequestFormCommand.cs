using PEMS.Application.Common.DTOs;

namespace PEMS.Application.Delegations.Commands;

/// <summary>
/// Common shape of the public UC-17 visit-request form, shared by the Initiate
/// (send-OTP) and VerifyAndCreate (submit) commands. Lets a single FluentValidation
/// rule set validate both steps, so the create step re-validates the full payload
/// server-side and never trusts that the form passed validation at the OTP step.
/// </summary>
public interface IVisitRequestFormCommand
{
    string RegisterFullName { get; }
    string RegisterNationality { get; }
    string RegisterOrganization { get; }
    string RegisterJobTitle { get; }
    string RegisterPhone { get; }
    string RegisterEmail { get; }

    string DelegationName { get; }
    string VisitScope { get; }
    IList<VisitSlotDto> VisitSlots { get; }
    string Purpose { get; }
    string? WorkingContent { get; }

    IList<VisitorDto> Visitors { get; }
    IList<SupportTeamMemberDto> SupportTeam { get; }

    ContactPointDto ContactPoint { get; }
    bool IsContactSelf { get; }

    string Language { get; }
    string? Vehicle { get; }
    string? Notes { get; }
}
