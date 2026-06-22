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
    string RegistrantFullName { get; }
    string RegistrantNationality { get; }
    string RegistrantOrganization { get; }
    string RegistrantPosition { get; }
    string RegistrantPhone { get; }
    string RegistrantEmail { get; }

    string DelegationName { get; }
    string VisitScope { get; }
    string VisitType { get; }
    string? VisitTypeOther { get; }
    IList<VisitSlotDto> CampusVisits { get; }
    string Purpose { get; }
    string? WorkingContent { get; }
    
    int ExpectedGuestCount { get; }
    IList<VisitorDto> Visitors { get; }
    IList<SupportTeamMemberDto> SupportMembers { get; }

    ContactPointDto ContactPerson { get; }
    bool IsContactSelf { get; }

    string WorkingLanguage { get; }
    string? InterpreterNote { get; }
    string TransportationType { get; }
    string? TransportationDetail { get; }
    string MediaConsentStatus { get; }
    string? MediaConsentNote { get; }
    ulong? PartnerId { get; }
    string? Notes { get; }
}
