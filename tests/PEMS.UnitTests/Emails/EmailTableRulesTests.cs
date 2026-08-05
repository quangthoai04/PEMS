using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Common;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// What a table in an email body may be (V4 §7.3, §17).
///
/// <para>
/// The editor's table dialog cannot produce a nested or oversized table — it has no raw-HTML field and
/// its row/column model has no way to express one. These tests exist because that is not the only way a
/// table arrives: pasted from Word, carried over from an older template, or posted straight at the API.
/// The frontend's constraints are an affordance; this is the rule.
/// </para>
/// </summary>
public class EmailTableRulesTests
{
    private static string Table(int rows, int cols)
    {
        var body = string.Concat(Enumerable.Range(0, rows).Select(r =>
            "<tr>" + string.Concat(Enumerable.Range(0, cols).Select(c => $"<td>{r}.{c}</td>")) + "</tr>"));
        return $"<table role=\"presentation\"><tbody>{body}</tbody></table>";
    }

    [Fact]
    public void An_ordinary_table_is_accepted()
    {
        Assert.Empty(EmailTableRules.Problems(Table(3, 2)));
        EmailTableRules.AssertUsable(Table(3, 2));   // does not throw
    }

    [Fact]
    public void Content_with_no_table_is_accepted()
    {
        Assert.Empty(EmailTableRules.Problems("<p>Kính gửi anh Nam,</p>"));
        Assert.Empty(EmailTableRules.Problems(null));
        Assert.Empty(EmailTableRules.Problems("   "));
    }

    /// <summary>
    /// Nested tables are refused rather than flattened. Flattening changes what the message SAYS: an
    /// author who pasted a nested table did not ask for its inner rows to be merged into the outer ones,
    /// and they would not see that it had happened until the mail was already delivered.
    /// </summary>
    [Fact]
    public void A_table_inside_a_table_is_refused()
    {
        var nested = "<table><tbody><tr><td>"
            + "<table><tbody><tr><td>trong</td></tr></tbody></table>"
            + "</td></tr></tbody></table>";

        var problems = EmailTableRules.Problems(nested);

        // Once, not twice — the inner table is reported through its parent.
        Assert.Single(problems);
        Assert.Contains("lồng", problems[0].Vi);
        Assert.Contains("nested", problems[0].En);
    }

    [Fact]
    public void Two_tables_side_by_side_are_not_mistaken_for_nesting()
    {
        Assert.Empty(EmailTableRules.Problems(Table(2, 2) + "<p>giữa</p>" + Table(2, 2)));
    }

    [Fact]
    public void A_table_past_the_column_ceiling_is_refused()
    {
        var problems = EmailTableRules.Problems(Table(2, EmailTableRules.MaxColumns + 1));

        Assert.Single(problems);
        Assert.Contains($"{EmailTableRules.MaxColumns + 1} cột", problems[0].Vi);
    }

    [Fact]
    public void Exactly_the_column_ceiling_is_still_allowed()
    {
        Assert.Empty(EmailTableRules.Problems(Table(3, EmailTableRules.MaxColumns)));
    }

    /// <summary>
    /// There is no row ceiling, and that is deliberate — see <see cref="EmailTableRules"/>.
    ///
    /// <para>
    /// The setup-progress email a Host edits before sending carries system-built tables with one row per
    /// guest, per agenda item and per logistics item. A row limit would have refused that send for a
    /// visit with twenty-one guests, and told the Host their TABLE was the problem when the only problem
    /// was the size of the delegation. Rows do not break mail rendering; width does.
    /// </para>
    /// </summary>
    [Fact]
    public void A_long_table_is_accepted_because_rows_do_not_break_mail()
    {
        Assert.Empty(EmailTableRules.Problems(Table(120, 4)));
    }

    [Fact]
    public void The_widest_row_decides_the_column_count()
    {
        var ragged = "<table><tbody><tr><td>a</td></tr><tr>"
            + string.Concat(Enumerable.Repeat("<td>x</td>", EmailTableRules.MaxColumns + 1))
            + "</tr></tbody></table>";

        Assert.Single(EmailTableRules.Problems(ragged));
    }

    [Fact]
    public void AssertUsable_throws_with_a_code_the_screen_can_act_on()
    {
        var nested = "<table><tbody><tr><td><table><tbody><tr><td>x</td></tr></tbody></table></td></tr></tbody></table>";

        var ex = Assert.Throws<ValidationException>(() => EmailTableRules.AssertUsable(nested));

        Assert.Equal(EmailErrorCodes.AuthoredTableUnsupported, ex.ErrorCode);
    }
}
