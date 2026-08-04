using System.Linq;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Application.Delegations.SetupProgressEmail;
using PEMS.Infrastructure.Security;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The email sanitiser against the tables the backend builds into a body.
///
/// <para>
/// <b>The defect.</b> An email body is sanitised on its way out, and the sanitiser removed <c>style</c>
/// from its allowed attributes. Every table in the setup-progress message declares its borders, padding,
/// widths and <c>table-layout:fixed</c> in inline CSS — and carries <c>border="0"</c> as the HTML
/// attribute — so what reached the recipient was a grid with no lines, no cell padding, and automatic
/// layout. That is the reported "mất border, lệch cột, header dính chữ".
/// </para>
/// <para>
/// It survived every existing test because nothing sanitised the block: the unit tests assert on
/// <see cref="VisitSetupEmailHtml"/>'s OUTPUT, which is correct, and the composer's preview is sanitised
/// by the FRONTEND, which keeps <c>style</c>. Preview and delivered mail were two different documents,
/// and only the one nobody rendered in a test was broken.
/// </para>
/// <para>
/// These run the REAL <see cref="HtmlSanitizerService"/> — the reason they live in the integration
/// project, which is the one that references Infrastructure.
/// </para>
/// </summary>
public class EmailBodySanitizerTableStyleTests
{
    private static readonly HtmlSanitizerService Sanitizer = new();

    /// <summary>
    /// Sanitises, then removes the whitespace the CSS parser adds.
    ///
    /// <para>
    /// Ganss reparses the declarations and prints them canonically — <c>border-collapse: collapse</c>
    /// rather than the <c>border-collapse:collapse</c> the renderer emitted. That is a formatting
    /// difference and nothing else; asserting on the renderer's exact spelling would fail for a reason
    /// that has no effect on any mail client.
    /// </para>
    /// </summary>
    private static string Clean(string html) =>
        Sanitizer.SanitizeEmailHtml(html).Replace(": ", ":").Replace("; ", ";");

    private static VisitSetupSnapshot Snapshot()
    {
        var report = new ScheduleReportDto
        {
            DelegationName = "Đoàn Đại học Kyoto",
            PlannedStartAt = new System.DateTime(2026, 8, 20, 9, 0, 0),
            PlannedEndAt = new System.DateTime(2026, 8, 20, 11, 30, 0),
            Location = "FPT University",
            Purpose = "Tham quan cơ sở",
        };

        report.GuestSide.Add(new ScheduleReportPersonDto
        { FullName = "Tanaka Hiro", Organization = "Kyoto Univ.", RoleLabel = "Khách mời" });
        report.Agenda.Add(new ScheduleReportAgendaRowDto
        {
            StartTime = new System.DateTime(2026, 8, 20, 9, 0, 0),
            EndTime = new System.DateTime(2026, 8, 20, 9, 30, 0),
            Title = "Đón đoàn tại sảnh",
            Description = "Chụp ảnh lưu niệm",
            Venue = "Sảnh Beta",
            Responsible = "Phòng Hợp tác Quốc tế",
        });

        return new VisitSetupSnapshot(
            report, "FPT University HCM", "Trao đổi hợp tác", null,
            new[]
            {
                new VisitSetupLogisticsRow("MEETING_ROOM", "Phòng họp Alpha", 1,
                    new System.DateTime(2026, 8, 20, 9, 0, 0),
                    new System.DateTime(2026, 8, 20, 11, 0, 0), "ACCEPTED"),
            },
            new System.DateTime(2026, 8, 19, 17, 0, 0));
    }

    /// <summary>
    /// The one assertion that would have caught it: the block still looks like a table AFTER sanitising.
    /// </summary>
    [Fact]
    public void The_setup_tables_keep_their_borders_and_layout_through_the_sanitizer()
    {
        var sanitized = Clean(VisitSetupEmailHtml.Render(Snapshot(), "vi"));

        Assert.Contains("border-collapse:collapse", sanitized);
        Assert.Contains("table-layout:fixed", sanitized);
        Assert.Contains("border:1px solid", sanitized);
        Assert.Contains("padding:6px 8px", sanitized);
        // The header row is styled cells, not <th>, so its background is the ONLY thing that makes it
        // read as a header. Stripped, the header became an ordinary first row. Asserted on the colour
        // rather than on `background:#f3f4f6`, because the parser may expand the shorthand into its
        // longhands — a different spelling of the same fill.
        Assert.Contains("#f3f4f6", sanitized);
    }

    [Fact]
    public void Column_widths_survive_in_both_the_colgroup_and_the_cells()
    {
        var sanitized = Clean(VisitSetupEmailHtml.Render(Snapshot(), "vi"));

        // The <colgroup> style is what a browser uses…
        Assert.Contains("width:18%", sanitized);
        Assert.Contains("width:42%", sanitized);
        // …and the width attribute is what Outlook's Word engine uses. Both have to come through.
        Assert.Contains("width=\"18%\"", sanitized);
        Assert.Contains("<colgroup>", sanitized);
    }

    [Fact]
    public void Wrapping_rules_survive_so_a_fixed_layout_cannot_overflow()
    {
        var sanitized = Clean(VisitSetupEmailHtml.Render(Snapshot(), "vi"));

        Assert.Contains("overflow-wrap:break-word", sanitized);
        Assert.Contains("word-break:normal", sanitized);
        Assert.Contains("white-space:normal", sanitized);
    }

    /// <summary>
    /// The allow-list is a LIST, not an open door. These are the properties that make an inline style
    /// worth attacking, and none of them is anything an email body needs.
    /// </summary>
    [Theory]
    [InlineData("position:fixed")]
    [InlineData("position:absolute")]
    [InlineData("z-index:9999")]
    public void Positioning_properties_are_still_dropped(string css)
    {
        var sanitized = Clean($"<div style=\"{css};color:#111\">x</div>");

        Assert.DoesNotContain(css.Split(':')[0], sanitized);
        // …while a legitimate neighbouring property in the same attribute is kept.
        Assert.Contains("color", sanitized);
    }

    [Fact]
    public void A_script_url_inside_a_style_is_dropped()
    {
        var sanitized = Sanitizer.SanitizeEmailHtml(
            "<div style=\"background:url(javascript:alert(1))\">x</div>");

        Assert.DoesNotContain("javascript", sanitized);
    }

    [Fact]
    public void Script_and_event_handlers_are_still_removed()
    {
        var sanitized = Sanitizer.SanitizeEmailHtml(
            "<div style=\"color:#111\" onclick=\"alert(1)\">x</div><script>alert(2)</script>");

        Assert.DoesNotContain("onclick", sanitized);
        Assert.DoesNotContain("<script", sanitized);
        Assert.Contains("color", sanitized);
    }

    /// <summary>
    /// Opaque colours come back out as hex.
    ///
    /// <para>
    /// The CSS parser rewrites every hex colour to <c>rgba(r, g, b, 1)</c>, which Outlook's Word engine
    /// does not understand and drops — so the border fix would have held everywhere EXCEPT the client
    /// these tables are laid out for, with a symptom indistinguishable from the original defect.
    /// </para>
    /// </summary>
    [Fact]
    public void Opaque_colours_are_written_as_hex_rather_than_rgba()
    {
        var sanitized = Sanitizer.SanitizeEmailHtml(
            "<table><tbody><tr><td style=\"border:1px solid #374151;color:#004c91\">x</td></tr></tbody></table>");

        Assert.Contains("#374151", sanitized);
        Assert.Contains("#004c91", sanitized);
        Assert.DoesNotContain("rgba", sanitized);
    }

    /// <summary>
    /// A translucent value keeps its notation: it cannot be expressed as hex, and flattening it to an
    /// opaque colour would turn the shell's faint shadow into a solid black bar.
    /// </summary>
    [Fact]
    public void A_translucent_colour_is_left_as_rgba()
    {
        var sanitized = Sanitizer.SanitizeEmailHtml(
            "<div style=\"box-shadow:0 2px 8px rgba(0,0,0,.08)\">x</div>");

        Assert.Contains("rgba", sanitized);
    }

    /// <summary>
    /// The general (non-email) sanitiser is unchanged: news and FAQ bodies are authored in a different
    /// editor for a different surface, and widening them was not part of this repair.
    /// </summary>
    [Fact]
    public void The_general_sanitizer_still_strips_style()
    {
        Assert.DoesNotContain("color", Sanitizer.Sanitize("<p style=\"color:red\">x</p>"));
    }
}
