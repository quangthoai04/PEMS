using System;
using System.Collections.Generic;
using PEMS.Application.Common.DTOs;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// visit_scope and has_mixed_campus_details answer two DIFFERENT questions and are routinely
/// confused: scope is "how many campuses", mixed is "does the content differ between them". A
/// two-campus request with identical content is MULTI_CAMPUS with mixed = 0, and these tests pin
/// that pair so neither value can be derived from the other. Both are server-derived; the client
/// never supplies either.
/// </summary>
public class VisitRequestV2ScopeDerivationTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 9, 0, 0);

    private static CampusVisitFormDto Campus(
        string code,
        string delegationName = "Đoàn ABC",
        string purpose = "Thăm",
        string opContactName = "Op Contact",
        DateTime? start = null)
    {
        var s = start ?? Start;
        return new CampusVisitFormDto(
            code, s, s.AddHours(2),
            delegationName, "MEETING", null, purpose, "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto(opContactName, "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null);
    }

    // ── scope: purely a function of how many DISTINCT campuses ───────────────────

    [Fact]
    public void One_campus_is_single_campus()
        => Assert.Equal(VisitScopes.SingleCampus,
            VisitRequestV2Canonical.ScopeOf(new[] { Campus("HN") }));

    [Fact]
    public void Two_campuses_are_multi_campus()
        => Assert.Equal(VisitScopes.MultiCampus,
            VisitRequestV2Canonical.ScopeOf(new[] { Campus("HN"), Campus("HCM") }));

    [Fact]
    public void Two_campuses_with_identical_content_are_still_multi_campus()
    {
        // Scope must not be inferred from whether the content differs.
        var campuses = new[] { Campus("HN"), Campus("HCM") };
        Assert.Equal(VisitScopes.MultiCampus, VisitRequestV2Canonical.ScopeOf(campuses));
        Assert.False(VisitRequestV2Canonical.ComputeHasMixed(campuses));
    }

    [Fact]
    public void The_same_campus_twice_is_not_multi_campus()
    {
        // Defence in depth: the validator rejects duplicates, but scope is derived from the
        // DISTINCT set so a duplicate could never be recorded as a cross-campus request.
        Assert.Equal(VisitScopes.SingleCampus,
            VisitRequestV2Canonical.ScopeOf(new[] { Campus("HN"), Campus("hn") }));
    }

    [Fact]
    public void Campus_codes_are_matched_case_insensitively_for_scope()
        => Assert.Equal(VisitScopes.MultiCampus,
            VisitRequestV2Canonical.ScopeOf(new[] { Campus("hn"), Campus("HCM") }));

    [Fact]
    public void Blank_campus_entries_do_not_inflate_the_scope()
        => Assert.Equal(VisitScopes.SingleCampus,
            VisitRequestV2Canonical.ScopeOf(new[] { Campus("HN"), Campus("  ") }));

    // ── has_mixed: purely a function of CONTENT, never of campus or schedule ─────

    [Fact]
    public void A_single_campus_is_never_mixed()
        => Assert.False(VisitRequestV2Canonical.ComputeHasMixed(new List<CampusVisitFormDto> { Campus("HN") }));

    [Fact]
    public void Differing_only_by_campus_and_schedule_is_not_mixed()
    {
        var campuses = new List<CampusVisitFormDto>
        {
            Campus("HN", start: Start),
            Campus("HCM", start: Start.AddDays(3)),
        };
        Assert.Equal(VisitScopes.MultiCampus, VisitRequestV2Canonical.ScopeOf(campuses));
        Assert.False(VisitRequestV2Canonical.ComputeHasMixed(campuses));
    }

    [Fact]
    public void A_different_delegation_name_makes_it_mixed()
    {
        var campuses = new List<CampusVisitFormDto> { Campus("HN"), Campus("HCM", delegationName: "Đoàn XYZ") };
        Assert.Equal(VisitScopes.MultiCampus, VisitRequestV2Canonical.ScopeOf(campuses));
        Assert.True(VisitRequestV2Canonical.ComputeHasMixed(campuses));
    }

    [Fact]
    public void A_different_purpose_makes_it_mixed()
        => Assert.True(VisitRequestV2Canonical.ComputeHasMixed(
            new List<CampusVisitFormDto> { Campus("HN"), Campus("HCM", purpose: "Ký kết") }));

    [Fact]
    public void A_different_operational_contact_makes_it_mixed()
        => Assert.True(VisitRequestV2Canonical.ComputeHasMixed(
            new List<CampusVisitFormDto> { Campus("HN"), Campus("HCM", opContactName: "Người khác") }));

    [Fact]
    public void A_different_member_set_makes_it_mixed()
    {
        var hn = Campus("HN");
        var hcm = new CampusVisitFormDto(
            "HCM", Start, Start.AddHours(2), "Đoàn ABC", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto>
            {
                new("Guest A", "VN", "Guest", "GuestOrg"),
                new("Guest B", "VN", "Guest", "GuestOrg"),
            },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null);

        Assert.True(VisitRequestV2Canonical.ComputeHasMixed(new List<CampusVisitFormDto> { hn, hcm }));
    }
}
