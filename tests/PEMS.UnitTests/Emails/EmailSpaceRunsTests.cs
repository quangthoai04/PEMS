using System.Linq;
using PEMS.Application.Emails.Common;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// Runs of spaces are refused, and nothing else is (V4 §7.1, §7.4).
///
/// <para>
/// The rule is easy to state and easy to implement wrongly. A pattern over raw HTML finds "runs" in the
/// indentation between two block elements, inside a <c>style</c> value and in the padding of a URL — none
/// of which a reader will ever see, and every one of which would refuse a save an author cannot repair.
/// Half of what follows is therefore about what must NOT be reported.
/// </para>
/// </summary>
public class EmailSpaceRunsTests
{
    private static bool Refuses(string html) => EmailSpaceRuns.Problems(html).Count > 0;

    // ── What it must catch ───────────────────────────────────────────────────

    [Fact]
    public void Plain_spaces_typed_between_two_words_are_a_run()
    {
        Assert.True(Refuses("<p>Cột A   Cột B</p>"));
    }

    /// <summary>
    /// The commonest case by far: the editor turns a typed run into non-breaking spaces on its way out, so
    /// this is what a stored body actually contains.
    /// </summary>
    [Fact]
    public void Non_breaking_spaces_are_a_run_and_are_the_worse_one()
    {
        Assert.True(Refuses("<p>Cột A&nbsp;&nbsp;&nbsp;Cột B</p>"));
    }

    [Theory]
    [InlineData("&#160;")]
    [InlineData("&#xA0;")]
    [InlineData("&#x00A0;")]
    public void Every_spelling_of_the_same_character_is_the_same_run(string entity)
    {
        Assert.True(Refuses($"<p>A{entity}{entity}{entity}B</p>"));
    }

    [Fact]
    public void A_mixture_of_plain_and_non_breaking_spaces_still_counts()
    {
        Assert.True(Refuses("<p>A &nbsp;&nbsp;B</p>"));
    }

    /// <summary>
    /// A cell is visible text like any other. It is called out because the fix must not reach for the
    /// table's markup — the structure is the author's and stays exactly as it was.
    /// </summary>
    [Fact]
    public void Visible_text_inside_a_table_cell_counts()
    {
        Assert.True(Refuses(
            "<table><tr><td style=\"padding:8px 12px\">A&nbsp;&nbsp;&nbsp;B</td></tr></table>"));
    }

    [Fact]
    public void A_run_at_the_head_of_a_paragraph_counts_when_it_is_non_breaking()
    {
        // Somebody indenting with the space bar. Nothing produces this by accident.
        Assert.True(Refuses("<p>&nbsp;&nbsp;&nbsp;&nbsp;Kính gửi Quý vị,</p>"));
    }

    [Fact]
    public void A_link_label_is_visible_text_too()
    {
        Assert.True(Refuses("<p><a href=\"https://pems.example.com\">Xem   chi tiết</a></p>"));
    }

    [Fact]
    public void Vietnamese_and_English_prose_are_judged_the_same_way()
    {
        Assert.True(Refuses("<p>Thời   gian</p>"));
        Assert.True(Refuses("<p>Start   time</p>"));
    }

    // ── What it must NOT catch ───────────────────────────────────────────────

    [Fact]
    public void Ordinary_single_spaced_prose_is_fine()
    {
        Assert.False(Refuses("<p>Kính gửi Quý vị, đây là nội dung bình thường.</p>"));
    }

    /// <summary>Two spaces are ordinary typing after a full stop, not a column.</summary>
    [Fact]
    public void A_double_space_is_not_a_run()
    {
        Assert.False(Refuses("<p>Xong.  Tiếp theo là phần hai.</p>"));
    }

    /// <summary>
    /// <b>The false positive that matters.</b> Formatted markup puts a newline and an indent between
    /// elements. Those are separate text nodes and were never one run; a scan over the raw string joins
    /// them and reports 62 offending fields across the 31 shipped templates, every one of them spurious.
    /// </summary>
    [Fact]
    public void Indentation_between_two_elements_is_not_a_run()
    {
        Assert.False(Refuses("<p>Xin chào</p>\n      <table>\n        <tr><td>A</td></tr>\n      </table>"));
    }

    [Fact]
    public void A_style_value_is_not_visible_text()
    {
        Assert.False(Refuses("<p style=\"margin:0   0   16px   0;padding:0\">Xin chào</p>"));
    }

    [Fact]
    public void A_url_is_not_visible_text()
    {
        Assert.False(Refuses("<p><a href=\"https://pems.example.com/a?x=1&amp;y=2%20%20%20z\">Chi tiết</a></p>"));
    }

    /// <summary>
    /// The dispatcher builds the action area, and its spacing is not an editorial decision. Reporting it
    /// would leave an author with an error and no way to repair it.
    /// </summary>
    [Fact]
    public void Text_the_system_owns_is_not_the_authors_problem()
    {
        Assert.False(Refuses("<div data-system-block=\"action\"><span>Chấp   nhận</span></div>"));
    }

    [Fact]
    public void An_empty_or_whitespace_only_body_has_nothing_to_report()
    {
        Assert.False(Refuses(""));
        Assert.False(Refuses("   "));
        Assert.False(Refuses("<p>\n   \n</p>"));
    }

    /// <summary>Malformed markup must not throw — content arrives pasted from anywhere.</summary>
    [Fact]
    public void Broken_markup_is_judged_rather_than_rejected()
    {
        Assert.False(Refuses("<p>Xin chào<div><span>chưa đóng"));
        Assert.True(Refuses("<p>A   B<div><span>chưa đóng"));
    }

    // ── The sentence an author reads ─────────────────────────────────────────

    [Fact]
    public void The_message_names_the_tools_that_do_work_and_quotes_the_offending_text()
    {
        var problem = Assert.Single(EmailSpaceRuns.Problems("<p>Cột A&nbsp;&nbsp;&nbsp;Cột B</p>"));

        Assert.Contains("căn lề, thụt lề hoặc bảng", problem.Vi);
        Assert.Contains("Cột A", problem.Vi);
        Assert.Contains("alignment, indentation or a table", problem.En);
    }

    /// <summary>One sentence per field, however many runs are in it. A list of forty is not a report.</summary>
    [Fact]
    public void Many_runs_produce_one_message()
    {
        Assert.Single(EmailSpaceRuns.Problems("<p>A   B</p><p>C   D</p><p>E   F</p>"));
    }

    // ── The send path ────────────────────────────────────────────────────────

    [Fact]
    public void AssertUsable_refuses_with_a_stable_code()
    {
        var ex = Assert.Throws<PEMS.Application.Common.Exceptions.ValidationException>(
            () => EmailSpaceRuns.AssertUsable("<p>A&nbsp;&nbsp;&nbsp;B</p>"));

        Assert.Equal("EMAIL_AUTHORED_CONSECUTIVE_SPACES_NOT_ALLOWED", ex.ErrorCode);
    }

    [Fact]
    public void AssertUsable_lets_clean_content_through()
    {
        EmailSpaceRuns.AssertUsable("<p>Kính gửi Quý vị,</p><table><tr><td>A</td><td>B</td></tr></table>");
    }

    // ── The shipped templates ────────────────────────────────────────────────

    /// <summary>
    /// Every default body and subject PEMS ships passes. Without this the rule could be introduced in a
    /// state where an operator cannot save the template they just opened — the failure mode of any
    /// validation added to content that already exists.
    /// </summary>
    [Fact]
    public void No_shipped_template_trips_the_rule()
    {
        var offenders = EmailTemplateDefaults.ByCode.Values
            .SelectMany(t => new[]
            {
                (t.TemplateCode, Field: "subjectVi", Text: t.SubjectVi),
                (t.TemplateCode, Field: "subjectEn", Text: t.SubjectEn),
                (t.TemplateCode, Field: "bodyVi", Text: t.BodyVi),
                (t.TemplateCode, Field: "bodyEn", Text: t.BodyEn),
            })
            .Where(x => EmailSpaceRuns.Problems(x.Text).Count > 0)
            .Select(x => $"{x.TemplateCode}.{x.Field}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "Shipped templates would be unsaveable: " + string.Join(", ", offenders));
    }
}
