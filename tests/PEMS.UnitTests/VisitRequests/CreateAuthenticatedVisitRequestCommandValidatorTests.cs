using System;
using System.Collections.Generic;
using FluentValidation.TestHelper;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.CreateAuthenticatedVisitRequest;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// Shape validation for the AUTHENTICATED visit-request create (actor-relation feature).
/// Pure validator rules only — role/campus-scope/host-candidate checks are DB-dependent and
/// live in the handler (covered by Integration Tests). The shared form rule set is the same
/// one the public UC-17 flow uses, so only the NEW campus-processing shape is tested here.
/// </summary>
public class CreateAuthenticatedVisitRequestCommandValidatorTests
{
    private readonly CreateAuthenticatedVisitRequestCommandValidator _validator = new();

    private static CreateAuthenticatedVisitRequestCommand ValidCommand(
        IList<CampusProcessingDto>? processing = null,
        string campusCode = "HN")
    {
        var start = DateTime.Now.AddDays(7).Date.AddHours(9);
        return new CreateAuthenticatedVisitRequestCommand(
            RegistrantFullName: "Nguyễn Văn Test",
            RegistrantNationality: "Việt Nam",
            RegistrantOrganization: "FPT University",
            RegistrantPosition: "IC Staff",
            RegistrantPhone: "0912345678",
            RegistrantEmail: "staff.test@fpt.edu.vn",
            DelegationName: "Đoàn kiểm thử UT",
            VisitScope: VisitScopes.SingleCampus,
            VisitType: "CAMPUS_TOUR",
            VisitTypeOther: null,
            CampusVisits: new List<VisitSlotDto> { new(campusCode, start, start.AddHours(4)) },
            Purpose: "Tham quan và trao đổi hợp tác",
            WorkingContent: null,
            Visitors: new List<VisitorDto>(),
            SupportMembers: new List<SupportTeamMemberDto>(),
            ContactPerson: new ContactPointDto("Trần Thị Đầu Mối", "Công ty ABC", "0987654321", "contact@example.com"),
            IsContactSelf: false,
            WorkingLanguage: "VI",
            TransportationNote: null,
            MediaConsentStatus: "DECLINED",
            MediaConsentNote: null,
            PartnerId: null,
            Notes: null,
            CampusProcessing: processing,
            ConfirmedHostConflict: false,
            SubmissionId: Guid.NewGuid().ToString());
    }

    [Fact]
    public void ValidCommand_NoProcessing_NoErrors()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SendForReview_NoHost_NoErrors()
    {
        var cmd = ValidCommand(new List<CampusProcessingDto>
        {
            new("HN", CampusSubmissionModes.SendForReview, null),
        });
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SelfHost_NoHostId_NoErrors()
    {
        var cmd = ValidCommand(new List<CampusProcessingDto>
        {
            new("HN", CampusSubmissionModes.SelfHost, null),
        });
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AssignHost_WithHostId_NoErrors()
    {
        var cmd = ValidCommand(new List<CampusProcessingDto>
        {
            new("HN", CampusSubmissionModes.AssignHost, 42UL),
        });
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AssignHost_WithoutHostId_HasError()
    {
        var cmd = ValidCommand(new List<CampusProcessingDto>
        {
            new("HN", CampusSubmissionModes.AssignHost, null),
        });
        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor("CampusProcessing[0].HostUserId");
    }

    [Fact]
    public void SendForReview_WithHostId_HasError()
    {
        var cmd = ValidCommand(new List<CampusProcessingDto>
        {
            new("HN", CampusSubmissionModes.SendForReview, 42UL),
        });
        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor("CampusProcessing[0].HostUserId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("APPROVE_NOW")]
    [InlineData("self_host")] // codes are case-sensitive by contract
    public void UnknownMode_HasError(string mode)
    {
        var cmd = ValidCommand(new List<CampusProcessingDto>
        {
            new("HN", mode, null),
        });
        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor("CampusProcessing[0].Mode");
    }

    [Fact]
    public void Processing_ForUnselectedCampus_HasError()
    {
        // Selected campus is HN; the processing entry references HCM.
        var cmd = ValidCommand(new List<CampusProcessingDto>
        {
            new("HCM", CampusSubmissionModes.SelfHost, null),
        });
        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor(x => x.CampusProcessing);
    }

    [Fact]
    public void Processing_DuplicateCampusEntries_HasError()
    {
        var cmd = ValidCommand(new List<CampusProcessingDto>
        {
            new("HN", CampusSubmissionModes.SelfHost, null),
            new("hn", CampusSubmissionModes.SendForReview, null),
        });
        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor(x => x.CampusProcessing);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    public void SubmissionId_Invalid_HasError(string submissionId)
    {
        var cmd = ValidCommand() with { SubmissionId = submissionId };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.SubmissionId);
    }

    [Fact]
    public void SharedFormRules_StillApply_EmptyDelegationName_HasError()
    {
        var cmd = ValidCommand() with { DelegationName = "" };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DelegationName);
    }
}
