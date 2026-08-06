using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation.TestHelper;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.InitiateVisitRequestV2;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// Public v2 initiate uses the SAME canonical structural validator as authenticated create-v2 (plan §5) —
/// NOT the v1 rules (which demand ≥3-hour visits and ≥1 support member). These pure validator tests prove
/// the v2 rules apply: 30-minute minimum (29m59s fails / 30m passes), zero support members is valid,
/// duplicate campuses and the &gt;10-campus ceiling are rejected, and OTHER requires a detail.
/// </summary>
public class InitiateVisitRequestV2CommandValidatorTests
{
    private readonly InitiateVisitRequestV2CommandValidator _validator = new();

    private static CampusVisitFormDto Campus(
        string code, DateTime start, TimeSpan duration,
        string visitType = "MEETING", string? visitTypeOther = null,
        IList<SupportTeamMemberDto>? support = null)
        => new(
            code, start, start.Add(duration), "Đoàn ABC", visitType, visitTypeOther, "Trao đổi hợp tác", "Nội dung",
            new List<VisitorDto> { new("Nguyễn Khách", "Việt Nam", "Giảng viên", "ĐH ABC") },
            support ?? new List<SupportTeamMemberDto>(),
            new ContactPointDto("Đầu Mối", "ĐH ABC", "Trưởng phòng Hợp tác", "+84912345678", "op@example.com"),
            "VI", null, "DECLINED", null, null);

    private static InitiateVisitRequestV2Command Command(params CampusVisitFormDto[] campuses)
        => new(new VisitRequestFormDataV2(
            Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Người Đăng Ký", "Việt Nam", "ĐH ABC", "Trưởng phòng", "+84912345678", "reg@example.com"),
            null,
            campuses.ToList()));

    private static readonly DateTime Base = DateTime.Now.AddDays(20).Date.AddHours(9);

    [Fact]
    public void Valid_single_campus_with_zero_support_passes()
    {
        var result = _validator.TestValidate(Command(Campus("HN", Base, TimeSpan.FromHours(3))));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Exactly_30_minutes_passes()
    {
        var result = _validator.TestValidate(Command(Campus("HN", Base, TimeSpan.FromMinutes(30))));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Under_30_minutes_fails_the_v2_minimum_not_the_v1_3h_rule()
    {
        var result = _validator.TestValidate(Command(
            Campus("HN", Base, TimeSpan.FromMinutes(30).Subtract(TimeSpan.FromSeconds(1)))));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Ninety_minute_visit_passes_which_the_v1_3h_rule_would_reject()
    {
        // 90 minutes is invalid under v1 (≥3h) but valid under v2 — proves the v2 validator, not v1, is used.
        var result = _validator.TestValidate(Command(Campus("HN", Base, TimeSpan.FromMinutes(90))));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Duplicate_campus_fails()
    {
        var result = _validator.TestValidate(Command(
            Campus("HN", Base, TimeSpan.FromHours(3)),
            Campus("HN", Base.AddDays(1), TimeSpan.FromHours(3))));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void More_than_ten_campuses_fails()
    {
        var campuses = Enumerable.Range(0, 11)
            .Select(i => Campus($"C{i}", Base.AddDays(i), TimeSpan.FromHours(3)))
            .ToArray();
        var result = _validator.TestValidate(Command(campuses));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Other_visit_type_without_detail_fails()
    {
        var result = _validator.TestValidate(Command(
            Campus("HN", Base, TimeSpan.FromHours(3), visitType: "OTHER", visitTypeOther: null)));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Bad_registrant_email_fails()
    {
        var cmd = new InitiateVisitRequestV2Command(new VisitRequestFormDataV2(
            Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Người ĐK", "Việt Nam", "ĐH ABC", "TP", "+84912345678", "not-an-email"),
            null,
            new List<CampusVisitFormDto> { Campus("HN", Base, TimeSpan.FromHours(3)) }));
        var result = _validator.TestValidate(cmd);
        Assert.False(result.IsValid);
    }
}
