using System;
using System.Collections.Generic;
using System.Net;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Application.Delegations.SetupProgressEmail;
using Xunit;

namespace PEMS.UnitTests.Delegations.SetupProgressEmail;

/// <summary>
/// What the setup-progress email body is allowed to say, and what it must never say.
///
/// <para>
/// The negative half matters more than the positive half. The snapshot is an allow-list — a field that
/// never reaches <see cref="VisitSetupSnapshot"/> cannot be rendered — so these assert that the
/// internal fields sitting next to the shareable ones in the database do not appear in the output even
/// as a substring.
/// </para>
/// </summary>
public class VisitSetupEmailHtmlTests
{
    private const string InternalBriefing = "NOI BO: Hieu truong se ghe qua luc 10h, chuan bi qua tang";
    private const string OfflineNote = "NOI BO: da goi dien cho anh Tuan phong hanh chinh";

    private static VisitSetupSnapshot Snapshot(
        IReadOnlyList<VisitSetupLogisticsRow>? logistics = null,
        string? transportationNote = "Đoàn cần xe 16 chỗ đón tại sân bay",
        string? workingContent = "Trao đổi hợp tác đào tạo")
        => new(
            new ScheduleReportDto
            {
                DelegationName = "Đoàn Đại học Kyoto",
                PlannedStartAt = new DateTime(2026, 8, 20, 9, 0, 0),
                PlannedEndAt = new DateTime(2026, 8, 20, 11, 30, 0),
                Location = "FPT University",
                Purpose = "Tham quan cơ sở và ký kết hợp tác",
                GuestSide =
                {
                    new ScheduleReportPersonDto { FullName = "Tanaka Hiro", Organization = "Kyoto Univ.", RoleLabel = "Khách mời" },
                    new ScheduleReportPersonDto { FullName = "", Organization = "Kyoto Univ.", RoleLabel = "Khách mời" },
                },
                FptSide =
                {
                    new ScheduleReportPersonDto { FullName = "Trần Cảnh", Organization = "Phòng IC", RoleLabel = "Host" },
                },
                Agenda =
                {
                    new ScheduleReportAgendaRowDto
                    {
                        StartTime = new DateTime(2026, 8, 20, 9, 0, 0),
                        EndTime = new DateTime(2026, 8, 20, 9, 30, 0),
                        Title = "Đón đoàn tại sảnh",
                        Description = "Chụp ảnh lưu niệm",
                        Venue = "Sảnh Beta",
                        Responsible = "Phòng Hợp tác Quốc tế",
                    },
                },
            },
            "FPT University HCM",
            workingContent,
            transportationNote,
            logistics ?? new[]
            {
                new VisitSetupLogisticsRow("MEETING_ROOM", "Phòng họp Alpha", 1,
                    new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 11, 0, 0), "ACCEPTED"),
            },
            new DateTime(2026, 8, 2, 14, 30, 0));

    /// <summary>
    /// What the recipient's mail client shows, which is not what the source string looks like.
    ///
    /// <para>
    /// <see cref="WebUtility.HtmlEncode"/> — the escaper this codebase uses everywhere it builds email
    /// HTML — rewrites U+00A0..U+00FF as numeric references, so "Đoàn" leaves the renderer as
    /// "&amp;#272;o&amp;#224;n" while "Trần" (whose vowels sit above U+00FF) passes through untouched.
    /// Both display identically. Asserting on the raw string would therefore make these tests pass or
    /// fail on which diacritic a fixture happens to use, so they assert on the decoded text; the one
    /// test that is genuinely about escaping reads the raw HTML instead.
    /// </para>
    /// </summary>
    private static string Seen(string html) => WebUtility.HtmlDecode(html);

    // ── What must be there ──────────────────────────────────────────────────

    [Fact]
    public void The_overview_states_the_delegation_campus_time_place_purpose_and_working_content()
    {
        var text = Seen(VisitSetupEmailHtml.Render(Snapshot(), "vi"));

        Assert.Contains("Đoàn Đại học Kyoto", text);
        Assert.Contains("FPT University HCM", text);
        Assert.Contains("09:00", text);
        Assert.Contains("20/08/2026", text);
        Assert.Contains("Tham quan cơ sở và ký kết hợp tác", text);
        Assert.Contains("Trao đổi hợp tác đào tạo", text);
    }

    [Fact]
    public void The_guest_list_shows_named_people_and_skips_a_blank_row()
    {
        var html = VisitSetupEmailHtml.Render(Snapshot(), "vi");

        Assert.Contains("Tanaka Hiro", Seen(html));
        // A nameless row is a half-finished form entry; echoing it back reads as a lost delegate.
        // Structural, so it reads the raw markup rather than the decoded text.
        Assert.DoesNotContain("<td style=\"border:1px solid #d1d5db;vertical-align:top\"></td>", html);
    }

    [Fact]
    public void The_participant_list_and_the_agenda_carry_the_party_in_charge()
    {
        var text = Seen(VisitSetupEmailHtml.Render(Snapshot(), "vi"));

        Assert.Contains("Trần Cảnh", text);
        Assert.Contains("Đón đoàn tại sảnh", text);
        Assert.Contains("Sảnh Beta", text);
        // The requirement is explicit that the schedule names who or which unit runs each item.
        Assert.Contains("Phòng Hợp tác Quốc tế", text);
    }

    [Fact]
    public void Preparation_items_show_a_business_status_not_the_raw_enum()
    {
        var html = VisitSetupEmailHtml.Render(Snapshot(), "vi");

        Assert.Contains("Phòng họp Alpha", Seen(html));
        Assert.Contains("Đang chuẩn bị", Seen(html));
        Assert.DoesNotContain("ACCEPTED", html);
    }

    [Fact]
    public void Additional_requests_and_the_snapshot_time_are_stated()
    {
        var text = Seen(VisitSetupEmailHtml.Render(Snapshot(), "vi"));

        Assert.Contains("Đoàn cần xe 16 chỗ đón tại sân bay", text);
        Assert.Contains("14:30 02/08/2026", text);
    }

    [Fact]
    public void An_empty_setup_says_so_instead_of_rendering_an_empty_table()
    {
        var s = Snapshot(logistics: Array.Empty<VisitSetupLogisticsRow>(), transportationNote: null);

        var text = Seen(VisitSetupEmailHtml.Render(s, "vi"));

        Assert.Contains("Chưa có hạng mục chuẩn bị nào", text);
        Assert.DoesNotContain("6. Yêu cầu bổ sung", text);
    }

    [Fact]
    public void English_renders_english_headings()
    {
        var text = Seen(VisitSetupEmailHtml.Render(Snapshot(), "en"));

        Assert.Contains("Visit overview", text);
        Assert.Contains("Detailed schedule", text);
        Assert.Contains("Party in charge", text);
        Assert.Contains("In progress", text);
        Assert.DoesNotContain("Lịch trình chi tiết", text);
    }

    // ── What must NOT be there ──────────────────────────────────────────────

    /// <summary>
    /// The snapshot has no field to carry any of these, so the assertion is really about the record
    /// staying an allow-list: if somebody adds <c>PreparationNote</c> to it "to be helpful", this fails.
    /// </summary>
    [Fact]
    public void Internal_notes_never_reach_the_body()
    {
        // Decoded, so that entity-encoded text cannot hide a leak from the assertion.
        var text = Seen(VisitSetupEmailHtml.Render(Snapshot(), "vi"));

        Assert.DoesNotContain(InternalBriefing, text);
        Assert.DoesNotContain(OfflineNote, text);
        Assert.DoesNotContain("NOI BO", text);
    }

    [Fact]
    public void The_snapshot_type_exposes_no_internal_field()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in typeof(VisitSetupSnapshot).GetProperties()) names.Add(p.Name);
        foreach (var p in typeof(VisitSetupLogisticsRow).GetProperties()) names.Add(p.Name);

        foreach (var forbidden in new[]
        {
            "PreparationNote", "OfflineCoordinationNote", "NoteToFptu", "MediaConsentNote",
            "AssignedToUserId", "RequestedBy", "ReceivedBy", "AuditLog", "DecisionNote",
        })
        {
            Assert.False(names.Contains(forbidden),
                $"{forbidden} reached the guest-facing snapshot; it is internal.");
        }
    }

    [Fact]
    public void Values_from_the_database_are_html_encoded()
    {
        var s = Snapshot(workingContent: "<script>alert(1)</script>");

        var html = VisitSetupEmailHtml.Render(s, "vi");

        // The block is injected verbatim into the body, so encoding has to happen here or a
        // delegation name is markup.
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
