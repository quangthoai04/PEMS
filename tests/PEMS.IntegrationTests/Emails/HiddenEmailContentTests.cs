using PEMS.Application.Emails.Common;
using PEMS.Infrastructure.Security;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Content that would arrive invisible is stripped of whatever makes it invisible (V4 §14.7).
///
/// <para>
/// <b>Why this matters here and not in an ordinary web page.</b> The sender approves a final preview, and
/// that approval is the whole basis on which the message is allowed to go out. Text they cannot see is
/// text they did not approve — so hidden content turns the approval into a formality. It is also how a
/// message is made to read one way to a person and another to a filter, and how a tracking marker is
/// carried past the one human who looked at the mail before it left.
/// </para>
/// <para>
/// <b>Values, not properties.</b> <c>display</c> and <c>opacity</c> stay on the CSS allow-list because the
/// action blocks are built from <c>display:inline-block</c> buttons and the branded shell uses opacity.
/// Removing the properties outright to stop the hiding values would flatten every button in every
/// template — so the property survives and only its hiding form is dropped.
/// </para>
/// </summary>
public class HiddenEmailContentTests
{
    private static string Sanitize(string html) => new HtmlSanitizerService().SanitizeEmailHtml(html);

    [Theory]
    [InlineData("display:none")]
    [InlineData("display: none")]
    [InlineData("DISPLAY:NONE")]
    [InlineData("visibility:hidden")]
    [InlineData("visibility: collapse")]
    [InlineData("opacity:0")]
    [InlineData("opacity: 0.0")]
    [InlineData("opacity:.0")]
    [InlineData("font-size:0")]
    [InlineData("font-size: 0px")]
    [InlineData("text-indent:-9999px")]
    public void Content_cannot_be_delivered_invisibly(string declaration)
    {
        var result = Sanitize($"<p style=\"{declaration}\">theo dõi</p>");

        // The words stay — removing them would be a second, different decision — but they arrive readable.
        Assert.Contains("theo dõi", result);
        Assert.DoesNotContain("display:none", result.Replace(" ", string.Empty),
            System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("visibility:hidden", result.Replace(" ", string.Empty),
            System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("opacity:0\"", result.Replace(" ", string.Empty),
            System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("font-size:0\"", result.Replace(" ", string.Empty),
            System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("text-indent:-", result.Replace(" ", string.Empty),
            System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The declaration is removed from a style attribute that carries other, legitimate declarations —
    /// dropping the whole attribute would take the message's formatting with it.
    /// </summary>
    [Fact]
    public void Only_the_hiding_declaration_is_removed_from_a_mixed_style()
    {
        var result = Sanitize("<p style=\"color:#374151;display:none;text-align:center\">xin chào</p>");

        Assert.Contains("color", result);
        Assert.Contains("text-align", result);
        Assert.DoesNotContain("display", result, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An attribute left with nothing in it is dropped rather than kept as <c>style=""</c>.</summary>
    [Fact]
    public void A_style_that_was_only_hiding_is_removed_entirely()
    {
        Assert.DoesNotContain("style", Sanitize("<p style=\"display:none\">xin chào</p>"),
            System.StringComparison.OrdinalIgnoreCase);
    }

    // ── What must NOT be broken ──────────────────────────────────────────────

    /// <summary>
    /// The values every action block and template depends on. A regex that matched these would be
    /// invisible in a unit run and obvious in a recipient's inbox.
    /// </summary>
    [Theory]
    [InlineData("display:inline-block")]
    [InlineData("display:block")]
    [InlineData("display:table-cell")]
    [InlineData("opacity:1")]
    [InlineData("opacity:0.8")]
    [InlineData("opacity:.85")]
    [InlineData("font-size:14px")]
    [InlineData("font-size:0.9em")]
    [InlineData("text-indent:16px")]
    public void Ordinary_layout_values_are_left_alone(string declaration)
    {
        var property = declaration.Split(':')[0];

        Assert.Contains(property, Sanitize($"<p style=\"{declaration}\">xin chào</p>"),
            System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A real action block survives intact: it is the one piece of markup in the system whose buttons are
    /// built from <c>display:inline-block</c>, and it is not the sender's to alter.
    /// </summary>
    [Fact]
    public void A_real_action_block_is_not_disturbed()
    {
        var block = EmailComposition.AcceptDeclineBlock(
            "https://pems.test/a/1", "https://pems.test/d/1");

        var result = Sanitize(block);

        Assert.Contains("Chấp nhận", result);
        Assert.Contains("Từ chối", result);
        Assert.Contains("inline-block", result);
    }

    /// <summary>
    /// The system-block node survives sanitising.
    ///
    /// <para>
    /// An edited body makes a round trip through the sanitiser between the sender pressing "Xem trước kết
    /// quả" and the send assembling the message. <c>data-system-block</c> is the only thing distinguishing
    /// "the sender put the action area in this paragraph" from an ordinary empty div, so a sanitiser that
    /// dropped the attribute would discard the position silently — and the buttons would go back to the
    /// bottom of the message with every test still green.
    /// </para>
    /// </summary>
    [Fact]
    public void The_system_block_node_survives_sanitising()
    {
        var sanitized = Sanitize("<p>a</p>" + EmailSystemBlockNodes.ActionNodeHtml + "<p>b</p>");

        Assert.True(
            EmailSystemBlockNodes.HasActionNode(sanitized),
            $"the node did not survive sanitising: {sanitized}");
    }

    /// <summary>
    /// …and it is still inert afterwards. Allowing the attribute must not become a way to smuggle markup:
    /// the node the sanitiser returns carries no link, no handler and no text.
    /// </summary>
    [Fact]
    public void The_surviving_node_carries_nothing_actionable()
    {
        var sanitized = Sanitize(
            "<div data-system-block=\"action\" onclick=\"steal()\">"
            + "<a href=\"javascript:alert(1)\">x</a></div>");

        Assert.DoesNotContain("onclick", sanitized, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", sanitized, System.StringComparison.OrdinalIgnoreCase);
    }
}
