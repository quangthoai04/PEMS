using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Application.Delegations.SetupProgressEmail;
using Xunit;

namespace PEMS.UnitTests.Delegations.SetupProgressEmail;

/// <summary>
/// The SHAPE of the setup tables, asserted against a parsed document rather than by string matching.
///
/// <para>
/// The reported defect — headings stacked one syllable wide, columns crushed to nothing — was invisible
/// to the tests that already existed, and would have stayed invisible however many more of them were
/// written, because they all ask <c>Contains()</c>. A table whose every cell is present and whose layout
/// is unusable contains exactly the same substrings as a correct one. What went wrong was structural:
/// no table declared a column width, so both the browser and the mail client fell back to automatic
/// layout and sized each column from its content. One long agenda description took the surplus and the
/// rest collapsed toward their minimum content width, which in Vietnamese is the longest single syllable.
/// </para>
/// <para>
/// So these parse the markup and assert on the tree: every table declares as many columns as it renders,
/// every row agrees with its header, the widths add up, and the properties that keep a fixed layout
/// honest are present. Parsing is also what makes the malformed-markup cases meaningful — a missing
/// <c>&lt;/td&gt;</c> fails here as a parse error instead of silently passing a substring check.
/// </para>
/// </summary>
public class VisitSetupEmailTableStructureTests
{
    // ── Fixtures ────────────────────────────────────────────────────────────

    private static ScheduleReportPersonDto Person(string name, string? org = "Kyoto Univ.", string? role = "Khách mời")
        => new() { FullName = name, Organization = org, RoleLabel = role };

    private static VisitSetupSnapshot Snapshot(
        IReadOnlyList<ScheduleReportPersonDto>? guests = null,
        IReadOnlyList<ScheduleReportPersonDto>? fpt = null,
        IReadOnlyList<ScheduleReportAgendaRowDto>? agenda = null,
        IReadOnlyList<VisitSetupLogisticsRow>? logistics = null,
        string? transportationNote = "Đoàn cần xe 16 chỗ đón tại sân bay",
        string? workingContent = "Trao đổi hợp tác đào tạo",
        string delegationName = "Đoàn Đại học Kyoto")
    {
        var report = new ScheduleReportDto
        {
            DelegationName = delegationName,
            PlannedStartAt = new DateTime(2026, 8, 20, 9, 0, 0),
            PlannedEndAt = new DateTime(2026, 8, 20, 11, 30, 0),
            Location = "FPT University",
            Purpose = "Tham quan cơ sở và ký kết hợp tác",
        };

        report.GuestSide.AddRange(guests ?? new[] { Person("Tanaka Hiro") });
        report.FptSide.AddRange(fpt ?? new[] { Person("Trần Cảnh", "Phòng IC", "Host") });
        report.Agenda.AddRange(agenda ?? new[]
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
        });

        return new VisitSetupSnapshot(
            report,
            "FPT University HCM",
            workingContent,
            transportationNote,
            logistics ?? new[]
            {
                new VisitSetupLogisticsRow("MEETING_ROOM", "Phòng họp Alpha", 1,
                    new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 11, 0, 0), "ACCEPTED"),
            },
            new DateTime(2026, 8, 2, 14, 30, 0));
    }

    /// <summary>
    /// Parses the block as a document. The renderer emits closed, self-closing markup on purpose, so a
    /// parse failure here IS the assertion: it means a tag was left open or a cell was not balanced.
    /// </summary>
    private static XElement Parse(string html)
    {
        try
        {
            return XElement.Parse("<root>" + html + "</root>", LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"The setup block is not well-formed markup: {ex.Message}\n\n{html}");
        }
    }

    private static IReadOnlyList<XElement> Tables(string html) => Parse(html).Descendants("table").ToList();

    private static double Percent(string? raw) =>
        double.Parse((raw ?? "").TrimEnd('%'), CultureInfo.InvariantCulture);

    private static string StyleOf(XElement el) => (string?)el.Attribute("style") ?? "";

    /// <summary>How many columns a cell occupies — 1 unless it declares a colspan.</summary>
    private static int ColumnSpan(XElement cell) =>
        int.TryParse((string?)cell.Attribute("colspan"), out var span) && span > 0 ? span : 1;

    // ── 1. Column count agrees everywhere ───────────────────────────────────

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void Every_table_declares_as_many_columns_as_it_renders(string language)
    {
        var tables = Tables(VisitSetupEmailHtml.Render(Snapshot(), language));

        Assert.NotEmpty(tables);

        foreach (var table in tables)
        {
            var declared = table.Elements("colgroup").Elements("col").Count();
            Assert.True(declared > 0, "A table declared no <colgroup>; automatic layout is what broke it.");

            // Every row — the header row included, since it is now an ordinary <tr> of styled <td>s —
            // must render exactly the declared number of columns. This is the check that would have
            // caught the collapsed header had the header been inside <tbody> at the time.
            var rows = table.Descendants("tbody").Elements("tr").ToList();
            Assert.NotEmpty(rows);
            foreach (var row in rows)
                Assert.Equal(declared, row.Elements("td").Sum(td => ColumnSpan(td)));
        }
    }

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void Column_widths_are_declared_on_every_cell_and_add_up_to_the_full_width(string language)
    {
        foreach (var table in Tables(VisitSetupEmailHtml.Render(Snapshot(), language)))
        {
            var widths = table.Elements("colgroup").Elements("col")
                .Select(c => (string?)c.Attribute("width")).ToList();

            // Outlook's Word engine honours neither <colgroup> nor table-layout, so the width has to be
            // on the cell as well or the fix simply does not reach that client.
            foreach (var cell in table.Descendants("th").Concat(table.Descendants("td")))
                Assert.False(string.IsNullOrEmpty((string?)cell.Attribute("width")),
                    "A cell carries no width attribute; Outlook would lay this table out automatically.");

            var total = widths.Sum(Percent);
            Assert.True(Math.Abs(total - 100d) < 0.001,
                $"Column widths sum to {total}%, not 100% — a fixed layout would distribute the remainder.");
        }
    }

    [Fact]
    public void Tables_use_a_fixed_layout_and_cells_wrap_instead_of_widening()
    {
        var html = VisitSetupEmailHtml.Render(Snapshot(), "vi");

        foreach (var table in Tables(html))
        {
            Assert.Contains("table-layout:fixed", StyleOf(table));
            Assert.Contains("border-collapse:collapse", StyleOf(table));

            foreach (var cell in table.Descendants("th").Concat(table.Descendants("td")))
            {
                var style = StyleOf(cell);
                // Under a fixed layout an unbreakable token cannot widen its column, so without this it
                // would overflow the border instead.
                Assert.Contains("word-break:break-word", style);
                Assert.Contains("overflow-wrap:break-word", style);
                // Padding on the cell, because cellpadding is the attribute Outlook applies unevenly.
                Assert.Contains("padding:", style);
            }
        }
    }

    [Fact]
    public void Padding_is_not_declared_twice()
    {
        // cellpadding AND CSS padding would compound in the clients that honour both, which is the
        // narrow-column symptom again by a different route.
        var html = VisitSetupEmailHtml.Render(Snapshot(), "vi");

        foreach (var table in Tables(html))
            Assert.Equal("0", (string?)table.Attribute("cellpadding"));
    }

    // ── 2. Header and data stay aligned under awkward data ──────────────────

    /// <summary>
    /// The case that produced the report: one cell far longer than the rest. Under automatic layout this
    /// is what took the surplus and crushed the neighbouring headings.
    /// </summary>
    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void A_very_long_value_does_not_change_the_column_layout(string language)
    {
        var longText = string.Join(" ", Enumerable.Repeat("Trung tâm Hợp tác Quốc tế và Phát triển Đào tạo", 12));

        var baseline = Tables(VisitSetupEmailHtml.Render(Snapshot(), language))
            .Select(t => t.Elements("colgroup").Elements("col")
                .Select(c => (string?)c.Attribute("width")).ToList()).ToList();

        var stressed = Tables(VisitSetupEmailHtml.Render(Snapshot(
            guests: new[] { Person(longText, longText, longText) },
            agenda: new[]
            {
                new ScheduleReportAgendaRowDto
                {
                    StartTime = new DateTime(2026, 8, 20, 9, 0, 0),
                    EndTime = new DateTime(2026, 8, 20, 9, 30, 0),
                    Title = longText,
                    Description = longText,
                    Venue = longText,
                    Responsible = longText,
                },
            }), language))
            .Select(t => t.Elements("colgroup").Elements("col")
                .Select(c => (string?)c.Attribute("width")).ToList()).ToList();

        Assert.Equal(baseline, stressed);
    }

    /// <summary>
    /// An unbroken token has no wrap opportunity of its own, so only the cell's wrapping keeps it inside
    /// the column. Asserted as structure — the row still has the right number of cells and the widths
    /// are untouched — because that is what a client would get wrong.
    /// </summary>
    [Fact]
    public void An_unbreakable_token_stays_inside_its_column()
    {
        var token = new string('A', 400);

        foreach (var table in Tables(VisitSetupEmailHtml.Render(
            Snapshot(guests: new[] { Person(token, token, token) }), "vi")))
        {
            foreach (var row in table.Descendants("tbody").Elements("tr"))
                Assert.Equal(table.Elements("colgroup").Elements("col").Count(), row.Elements("td").Count());
        }
    }

    [Fact]
    public void Empty_and_missing_values_still_produce_a_full_row()
    {
        var html = VisitSetupEmailHtml.Render(Snapshot(
            guests: new[] { Person("Tanaka Hiro", org: null, role: null) },
            agenda: new[]
            {
                new ScheduleReportAgendaRowDto
                {
                    // StartTime is non-nullable on the DTO; an open-ended item is one with no EndTime.
                    StartTime = new DateTime(2026, 8, 20, 9, 0, 0), EndTime = null,
                    Title = "Chưa đặt tên", Description = null, Venue = "", Responsible = "",
                },
            },
            logistics: new[] { new VisitSetupLogisticsRow("OTHER", "Hạng mục", null, null, null, "UNKNOWN") }),
            "vi");

        foreach (var table in Tables(html))
        {
            var declared = table.Elements("colgroup").Elements("col").Count();
            foreach (var row in table.Descendants("tbody").Elements("tr"))
                // A blank value must still be a cell. Dropping it shifts every value after it one column
                // left, which is the alignment defect the report also describes.
                Assert.Equal(declared, row.Elements("td").Count());
        }
    }

    // ── 3. Content and safety survive the restructure ───────────────────────

    [Fact]
    public void Hostile_markup_in_a_value_is_text_and_not_a_cell()
    {
        var html = VisitSetupEmailHtml.Render(Snapshot(
            guests: new[] { Person("</td></tr><tr><td>injected", "<script>alert(1)</script>", "<b>role</b>") }),
            "vi");

        // Parsing is the assertion: if the closing tags in the value had been taken as markup, the
        // document would be unbalanced and the row counts below would be wrong.
        foreach (var table in Tables(html))
        {
            var declared = table.Elements("colgroup").Elements("col").Count();
            foreach (var row in table.Descendants("tbody").Elements("tr"))
                Assert.Equal(declared, row.Elements("td").Count());
        }

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void The_headings_and_their_data_stay_in_the_same_order(string language)
    {
        var en = language == "en";
        var html = VisitSetupEmailHtml.Render(Snapshot(), language);

        var people = Tables(html).First(t =>
            t.Descendants("td").Any(td => td.Value == (en ? "Organisation" : "Đơn vị")));

        var rows = people.Descendants("tbody").Elements("tr").ToList();

        // The header is the first row, made of styled <td>s — see the <th> regression test below.
        var headers = rows[0].Elements("td").Select(td => td.Value).ToList();
        Assert.Equal(en
            ? new[] { "Name", "Organisation", "Role" }
            : new[] { "Họ tên", "Đơn vị", "Vai trò" }, headers);

        var firstRow = rows[1].Elements("td").ToList();
        Assert.Equal("Tanaka Hiro", firstRow[0].Value);
        Assert.Equal("Kyoto Univ.", firstRow[1].Value);
        Assert.Equal("Khách mời", firstRow[2].Value);
    }

    /// <summary>
    /// No table header markup, anywhere, in either language.
    ///
    /// <para>
    /// The reported defect: the Host opened "Gửi cập nhật chuẩn bị" and the header row of every
    /// multi-column table had collapsed into one cell — "Họ tênĐơn vịVai trò", "Thời gianNội dungĐịa
    /// điểmPhụ trách" — while the data rows below kept their columns. This block is written into a draft
    /// the Host then edits in the rich-text editor, and the editor's document model has no table header:
    /// it drops &lt;thead&gt; and &lt;th&gt; and keeps the text that was inside them, so three header
    /// cells became one run of text. &lt;td&gt; is understood, which is why only the header broke and
    /// why it looked like a styling problem rather than a lost tag.
    /// </para>
    /// <para>
    /// Asserted as an absolute rather than by re-checking the rendering, because the semantic markup is
    /// the natural thing to reach for and nothing else in the file would object to it coming back.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void No_table_uses_thead_or_th_because_the_composer_editor_drops_them(string language)
    {
        var html = VisitSetupEmailHtml.Render(Snapshot(), language);

        Assert.DoesNotContain("<thead", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<th ", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<th>", html, StringComparison.OrdinalIgnoreCase);

        foreach (var table in Tables(html))
        {
            Assert.Empty(table.Descendants("th"));
            Assert.Empty(table.Descendants("thead"));
        }
    }

    /// <summary>
    /// The header row must still LOOK like a header. Dropping &lt;th&gt; is only safe because the
    /// styling that made it read as a header lives on the cell.
    /// </summary>
    [Fact]
    public void The_header_row_still_carries_header_styling()
    {
        var html = VisitSetupEmailHtml.Render(Snapshot(), "vi");

        var people = Tables(html).First(t => t.Descendants("td").Any(td => td.Value == "Đơn vị"));
        var headerRow = people.Descendants("tbody").Elements("tr").First();

        foreach (var cell in headerRow.Elements("td"))
        {
            var style = StyleOf(cell);
            Assert.Contains("font-weight:bold", style);
            Assert.Contains("background:", style);
        }
    }

    [Fact]
    public void A_table_with_no_rows_is_replaced_by_a_sentence_rather_than_an_empty_grid()
    {
        var html = VisitSetupEmailHtml.Render(Snapshot(
            guests: Array.Empty<ScheduleReportPersonDto>(),
            logistics: Array.Empty<VisitSetupLogisticsRow>(),
            transportationNote: null), "vi");

        foreach (var table in Tables(html))
            Assert.NotEmpty(table.Descendants("tbody").Elements("tr"));
    }
}
