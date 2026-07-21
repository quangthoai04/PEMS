using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequestV2;
using PEMS.Application.Delegations.Commands.UpdatePendingVisitRequestV2;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// The whole point of §3: the required-field contract must be the SAME on every write path, not just
/// create. Pending-edit and resubmit previously only null-checked the registrant/primary contact, and
/// the amendment left working content, the operational contact and support-member rows unvalidated —
/// so a request that could not be CREATED with blank fields could be EDITED, RESUBMITTED or AMENDED
/// into that state. These tests pin that the same rejections now apply to each path.
/// </summary>
public class VisitRequestV2WritePathParityTests
{
    private static readonly DateTime Start = DateTime.Now.AddDays(20);

    private static ContactPointDto Op(string org = "ĐH X", string phone = "+84911111111", string email = "op@example.com")
        => new("ĐM CS", org, phone, email);

    private static CampusVisitEditV2Dto EditCampus(
        string? workingContent = "Nội dung làm việc",
        ContactPointDto? op = null,
        IList<SupportTeamMemberDto>? support = null,
        ulong? instanceId = 10, int? rowVersion = 0)
        => new(
            instanceId, rowVersion,
            "HN", Start, Start.AddHours(2),
            "Đoàn A", "MEETING", null, "Trao đổi", workingContent,
            new List<VisitorDto> { new("Khách 1", "VN", "GV", "ĐH X") },
            support ?? new List<SupportTeamMemberDto>(),
            op ?? Op(),
            "EN", null, "DECLINED", null, null);

    private static VisitRequestEditV2Dto Edit(
        CampusVisitEditV2Dto? campus = null,
        RegistrantInputV2? registrant = null,
        ContactPointDto? primaryContact = null)
        => new(
            0,
            registrant ?? new RegistrantInputV2("Người ĐK", "VN", "ĐH X", "TP", "+84912345678", "reg@example.com"),
            primaryContact ?? new ContactPointDto("ĐM", "ĐH X", "+84987654321", "contact@example.com"),
            null,
            new List<CampusVisitEditV2Dto> { campus ?? EditCampus() });

    private static VisitAmendmentProposalDto Proposal(
        string? workingContent = "Nội dung làm việc",
        ContactPointDto? op = null,
        IList<SupportTeamMemberDto>? support = null)
        => new(
            0, 1, 1, null,
            "Đoàn A", "MEETING", null, "Trao đổi", workingContent, "EN",
            op ?? Op(),
            new List<VisitorDto> { new("Khách 1", "VN", "GV", "ĐH X") },
            support ?? new List<SupportTeamMemberDto>(),
            Start, Start.AddHours(2));

    private static readonly UpdatePendingVisitRequestV2CommandValidator PendingEdit = new();
    private static readonly ResubmitRejectedVisitRequestV2CommandValidator Resubmit = new();
    private static readonly SubmitVisitAmendmentCommandValidator Amendment = new();

    private static string[] EditErrors(IValidator<UpdatePendingVisitRequestV2Command> v, VisitRequestEditV2Dto edit)
        => v.Validate(new UpdatePendingVisitRequestV2Command(1, edit)).Errors.Select(e => e.PropertyName).ToArray();
    private static string[] ResubmitErrors(VisitRequestEditV2Dto edit)
        => Resubmit.Validate(new ResubmitRejectedVisitRequestV2Command(1, edit)).Errors.Select(e => e.PropertyName).ToArray();
    private static string[] AmendErrors(VisitAmendmentProposalDto p)
        => Amendment.Validate(new SubmitVisitAmendmentCommand(1, 1, p)).Errors.Select(e => e.PropertyName).ToArray();

    // ── Baselines ────────────────────────────────────────────────────────────

    [Fact]
    public void Pending_edit_baseline_is_valid()
        => Assert.True(PendingEdit.Validate(new UpdatePendingVisitRequestV2Command(1, Edit())).IsValid);

    [Fact]
    public void Resubmit_baseline_is_valid()
        => Assert.True(Resubmit.Validate(new ResubmitRejectedVisitRequestV2Command(1, Edit())).IsValid);

    [Fact]
    public void Amendment_baseline_is_valid()
        => Assert.True(Amendment.Validate(new SubmitVisitAmendmentCommand(1, 1, Proposal())).IsValid);

    // ── Working content required everywhere ──────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Pending_edit_requires_working_content(string? value)
        => Assert.Contains(EditErrors(PendingEdit, Edit(EditCampus(workingContent: value))), p => p.Contains("WorkingContent"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resubmit_requires_working_content(string? value)
        => Assert.Contains(ResubmitErrors(Edit(EditCampus(workingContent: value))), p => p.Contains("WorkingContent"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Amendment_requires_working_content(string? value)
        => Assert.Contains(AmendErrors(Proposal(workingContent: value)), p => p.Contains("WorkingContent"));

    // ── Operational contact must be complete everywhere ──────────────────────

    [Fact]
    public void Pending_edit_requires_a_complete_operational_contact()
        => Assert.NotEmpty(EditErrors(PendingEdit, Edit(EditCampus(op: Op(org: "", email: "")))));

    [Fact]
    public void Amendment_requires_a_complete_operational_contact()
        => Assert.NotEmpty(AmendErrors(Proposal(op: Op(org: ""))));

    [Fact]
    public void Amendment_rejects_a_non_phone_operational_contact()
        => Assert.Contains(AmendErrors(Proposal(op: Op(phone: "090abc123"))), p => p.Contains("Phone"));

    // ── Registrant / primary contact required on edit + resubmit ─────────────

    [Fact]
    public void Pending_edit_requires_registrant_job_title()
        => Assert.Contains(
            EditErrors(PendingEdit, Edit(registrant: new RegistrantInputV2("Người ĐK", "VN", "ĐH X", "", "+84912345678", "reg@example.com"))),
            p => p.Contains("JobTitle"));

    [Fact]
    public void Resubmit_requires_registrant_nationality()
        => Assert.Contains(
            ResubmitErrors(Edit(registrant: new RegistrantInputV2("Người ĐK", "", "ĐH X", "TP", "+84912345678", "reg@example.com"))),
            p => p.Contains("Nationality"));

    [Fact]
    public void Resubmit_requires_a_primary_contact_organization()
        => Assert.Contains(
            ResubmitErrors(Edit(primaryContact: new ContactPointDto("ĐM", "", "+84987654321", "contact@example.com"))),
            p => p.Contains("Organization"));

    [Fact]
    public void Pending_edit_rejects_a_non_phone_primary_contact()
        => Assert.Contains(
            EditErrors(PendingEdit, Edit(primaryContact: new ContactPointDto("ĐM", "ĐH X", "090abc123", "contact@example.com"))),
            p => p.Contains("Phone"));

    // ── Support rows: optional list, complete rows ───────────────────────────

    [Fact]
    public void Pending_edit_allows_an_empty_support_list()
        => Assert.True(PendingEdit.Validate(new UpdatePendingVisitRequestV2Command(
            1, Edit(EditCampus(support: new List<SupportTeamMemberDto>())))).IsValid);

    [Fact]
    public void Pending_edit_rejects_a_half_filled_support_row()
        => Assert.NotEmpty(EditErrors(PendingEdit, Edit(EditCampus(
            support: new List<SupportTeamMemberDto> { new("Hỗ trợ 1", "", "", "") }))));

    [Fact]
    public void Amendment_rejects_a_half_filled_support_row()
        => Assert.NotEmpty(AmendErrors(Proposal(
            support: new List<SupportTeamMemberDto> { new("Hỗ trợ 1", "", "", "") })));
}
