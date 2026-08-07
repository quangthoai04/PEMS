using PEMS.Infrastructure.Security;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// One contract, two sides: every format the email editor offers survives the sanitiser, and everything
/// the editor refuses to produce is still removed (V4 §29, §49).
///
/// <para>
/// <b>Why this pairing needs a test of its own.</b> The editor's toolbar and the sanitiser's allow-list
/// are written in different languages, in different projects, by people looking at different problems —
/// and only one of them is visible while authoring. When they disagree the failure is silent and lands on
/// the recipient: an operator centres a heading, saves, previews (the FRONTEND sanitiser keeps style), and
/// the delivered mail is left-aligned because the BACKEND sanitiser dropped the attribute. That exact
/// shape of defect is what <see cref="EmailBodySanitizerTableStyleTests"/> was written for, on the tables
/// the backend builds; this covers the other author — the person typing.
/// </para>
/// <para>
/// Each fragment below is what the editor actually emits, in Quill's own spelling: `rgb()` colours,
/// `font-size: 18px` with a space, the divider's inline border, the generated table's cell styles.
/// </para>
/// </summary>
public sealed class EmailEditorFormatSanitizerParityTests
{
    private static readonly HtmlSanitizerService Sanitizer = new();

    /// <summary>
    /// Sanitises and removes the whitespace the CSS parser adds, for the same reason the table suite
    /// does: Ganss reprints declarations canonically, and asserting on the editor's exact spacing would
    /// fail for a difference no mail client can see.
    /// </summary>
    private static string Clean(string html) =>
        Sanitizer.SanitizeEmailHtml(html).Replace(": ", ":").Replace("; ", ";");

    // ── What the toolbar produces must reach the recipient ────────────────────

    [Theory]
    [InlineData("<p><strong>đậm</strong></p>", "<strong>")]
    [InlineData("<p><em>nghiêng</em></p>", "<em>")]
    [InlineData("<p><u>gạch chân</u></p>", "<u>")]
    [InlineData("<p><s>gạch ngang</s></p>", "<s>")]
    [InlineData("<p><span style=\"font-family: Georgia;\">phông</span></p>", "font-family:Georgia")]
    [InlineData("<p><span style=\"font-size: 18px;\">cỡ chữ</span></p>", "font-size:18px")]
    // Opaque colours are re-spelled as hex by the sanitiser (pinned by EmailBodySanitizerTableStyleTests);
    // what matters is that the COLOUR survives, not which of the two notations it arrives in.
    [InlineData("<p><span style=\"color: rgb(255, 0, 0);\">màu chữ</span></p>", "color:#ff0000")]
    [InlineData("<p><span style=\"background-color: rgb(255, 255, 0);\">nền</span></p>", "background-color:#ffff00")]
    [InlineData("<p style=\"text-align: center;\">giữa</p>", "text-align:center")]
    [InlineData("<p style=\"text-align: right;\">phải</p>", "text-align:right")]
    [InlineData("<p style=\"margin-left: 16px;\">thụt lề</p>", "margin-left:16px")]
    [InlineData("<ul><li>một</li></ul>", "<ul>")]
    [InlineData("<ol><li>một</li></ol>", "<ol>")]
    [InlineData("<hr style=\"border:none;border-top:1px solid #e2e8f0;margin:20px 0\">", "border-top:1px solid")]
    [InlineData("<p><a href=\"https://pems.fpt.edu.vn/x\">liên kết</a></p>", "https://pems.fpt.edu.vn/x")]
    [InlineData("<p><a href=\"mailto:ai@fpt.edu.vn\">thư</a></p>", "mailto:ai@fpt.edu.vn")]
    public void A_format_the_editor_offers_survives_the_sanitizer(string html, string expected)
    {
        Assert.Contains(expected, Clean(html));
    }

    /// <summary>
    /// The generated table, whole: the borders and padding are the only thing that makes it READ as a
    /// table in mail, where there is no stylesheet to fall back on.
    /// </summary>
    [Fact]
    public void The_editors_table_keeps_its_structure_and_its_inline_css()
    {
        const string table =
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\""
            + " style=\"border-collapse:collapse;width:100%;margin:16px 0\"><tbody>"
            + "<tr><th style=\"border:1px solid #dbe4ee;padding:8px 10px;vertical-align:top;"
            + "background:#f8fafc;font-weight:600;text-align:left\">Hạng mục</th></tr>"
            + "<tr><td style=\"border:1px solid #dbe4ee;padding:8px 10px;vertical-align:top\">Ghế</td></tr>"
            + "</tbody></table>";

        var clean = Clean(table);

        Assert.Contains("<table", clean);
        Assert.Contains("role=\"presentation\"", clean);
        Assert.Contains("border-collapse:collapse", clean);
        Assert.Contains("border:1px solid #dbe4ee", clean);
        Assert.Contains("padding:8px 10px", clean);
        Assert.Contains("<th", clean);
        Assert.Contains("<td", clean);
        Assert.Contains("Hạng mục", clean);
    }

    /// <summary>
    /// The alignment attribute Outlook honours, alongside the margin everything else honours. Dropping
    /// either one leaves a centred table drifting left in exactly one family of mail clients.
    /// </summary>
    [Fact]
    public void A_centred_table_keeps_both_the_attribute_and_the_margin()
    {
        var clean = Clean(
            "<table role=\"presentation\" width=\"50%\" align=\"center\""
            + " style=\"border-collapse:collapse;width:50%;margin:16px auto\"><tbody>"
            + "<tr><td style=\"border:1px solid #dbe4ee\">ô</td></tr></tbody></table>");

        Assert.Contains("align=\"center\"", clean);
        Assert.Contains("margin:16px auto", clean);
        Assert.Contains("width:50%", clean);
    }

    // ── …and the sanitiser is still a sanitiser ──────────────────────────────

    [Theory]
    [InlineData("<p>a</p><script>alert(1)</script>", "alert(1)")]
    [InlineData("<p>a</p><iframe src=\"https://evil\"></iframe>", "<iframe")]
    [InlineData("<p onclick=\"steal()\">a</p>", "onclick")]
    [InlineData("<p><a href=\"javascript:steal()\">a</a></p>", "javascript:")]
    [InlineData("<p style=\"position:absolute;top:0\">a</p>", "position")]
    [InlineData("<p style=\"display:none\">a</p>", "display:none")]
    [InlineData("<p style=\"font-size:0\">a</p>", "font-size:0")]
    public void What_an_email_may_not_carry_is_still_removed(string html, string forbidden)
    {
        Assert.DoesNotContain(forbidden, Clean(html));
    }

    /// <summary>
    /// The system block's position marker survives, because the renderer needs it to put the real block
    /// where the author left it — and it is inert by construction: no href, no token, no text.
    /// </summary>
    [Fact]
    public void The_action_position_node_survives_for_the_flow_that_uses_it()
    {
        var clean = Clean("<p>a</p><div data-system-block=\"action\"></div><p>b</p>");

        Assert.Contains("data-system-block", clean);
    }
}
