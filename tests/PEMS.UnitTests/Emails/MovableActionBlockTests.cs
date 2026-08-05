using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Common;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The action block keeps the position the message gives it, and the sender may move it — but may not
/// duplicate it, forge one, or make it do anything.
///
/// <para>
/// <b>The defect these pin.</b> An edited message used to have its action area cut out and the real block
/// APPENDED at the end. A template reading "chọn một phương án bên dưới để chúng tôi tiếp tục xử lý"
/// followed by the buttons therefore arrived with that sentence pointing at a signature, and the buttons
/// below it — and the sender who noticed could not fix it, because the thing they needed to move was not
/// in their copy of the message. Position was silently the system's decision (V4 §9.1, §9.3, §12).
/// </para>
/// </summary>
public class MovableActionBlockTests
{
    private const string Block =
        EmailComposition.ActionBlockStart + "<div>REAL BUTTONS</div>" + EmailComposition.ActionBlockEnd;

    private static string BodyWithBlockInTheMiddle() =>
        "<p>Kính gửi anh Nam,</p>" + Block + "<p>Trân trọng,</p>";

    // ── The node stands in for the block, in place ───────────────────────────

    [Fact]
    public void The_rendered_block_becomes_a_node_where_it_already_stood()
    {
        var editable = EmailSystemBlockNodes.ReplaceInjectedBlockWithNode(BodyWithBlockInTheMiddle());

        Assert.Equal(
            "<p>Kính gửi anh Nam,</p>" + EmailSystemBlockNodes.ActionNodeHtml + "<p>Trân trọng,</p>",
            editable);

        // The live markup is gone: an editor must never be handed a real one-time URL to edit.
        Assert.DoesNotContain("REAL BUTTONS", editable);
        Assert.DoesNotContain("PEMS_ACTION_BLOCK", editable);
    }

    [Fact]
    public void The_block_returns_to_the_position_the_sender_left_the_node_in()
    {
        // The sender moved the action area ABOVE the greeting.
        var moved = EmailSystemBlockNodes.ActionNodeHtml + "<p>Kính gửi anh Nam,</p><p>Trân trọng,</p>";

        Assert.True(EmailSystemBlockNodes.TrySubstituteActionNode(moved, Block, out var result));

        Assert.Equal(Block + "<p>Kính gửi anh Nam,</p><p>Trân trọng,</p>", result);
        // …and specifically NOT at the end, which is where the old behaviour put it.
        Assert.StartsWith(EmailComposition.ActionBlockStart, result);
    }

    [Fact]
    public void Content_with_no_node_reports_so_rather_than_guessing_a_position()
    {
        var plain = "<p>Không có khối hành động.</p>";

        Assert.False(EmailSystemBlockNodes.TrySubstituteActionNode(plain, Block, out var result));
        Assert.Equal(plain, result);
    }

    /// <summary>
    /// Attribute order, quoting and stray whitespace vary once content has been through a browser editor
    /// and a sanitiser. Refusing those spellings would report "you deleted the buttons" to a sender who
    /// did nothing but drag them.
    /// </summary>
    [Theory]
    [InlineData("<div data-system-block=\"action\"></div>")]
    [InlineData("<div data-system-block='action'></div>")]
    [InlineData("<div class=\"x\" data-system-block=\"action\"></div>")]
    [InlineData("<div data-system-block=\"action\" class=\"x\"></div>")]
    [InlineData("<div data-system-block=\"action\">   </div>")]
    [InlineData("<div  DATA-SYSTEM-BLOCK = \"action\" ></div >")]
    public void A_node_is_recognised_however_the_editor_respelled_it(string node)
    {
        Assert.True(EmailSystemBlockNodes.HasActionNode(node));
        Assert.True(EmailSystemBlockNodes.TrySubstituteActionNode(node, Block, out var result));
        Assert.Equal(Block, result);
    }

    /// <summary>
    /// The editor draws a human-readable label inside its copy of the node so the sender can see what they
    /// are dragging. The frontend normalises that away before sending — but if it ever failed to, the
    /// worst outcome available is that the node stops matching here: the block would be appended at the
    /// end instead, AND the editor's own label would be delivered to the recipient as part of the message.
    ///
    /// <para>
    /// So the pattern deliberately matches a node WITH text in it, and consumes that text along with the
    /// node. The label is a rendering affordance, never content.
    /// </para>
    /// </summary>
    [Fact]
    public void A_node_still_carrying_the_editor_label_is_matched_and_the_label_consumed()
    {
        const string EditorForm =
            "<div class=\"pems-system-action-block\" data-system-block=\"action\" contenteditable=\"false\">"
            + "Khối nút phản hồi — hệ thống tự gắn khi gửi</div>";

        Assert.True(EmailSystemBlockNodes.HasActionNode(EditorForm));
        Assert.Equal(1, EmailSystemBlockNodes.CountActionNodes(EditorForm));

        Assert.True(EmailSystemBlockNodes.TrySubstituteActionNode(
            "<p>Xin chào,</p>" + EditorForm + "<p>Trân trọng,</p>", Block, out var result));

        Assert.Contains(Block, result);
        Assert.DoesNotContain("Khối nút phản hồi", result);
        Assert.DoesNotContain("pems-system-action-block", result);
    }

    /// <summary>
    /// The content of a node is TEXT, never nested markup — so the pattern uses <c>[^&lt;]*</c>. A lazy
    /// any-character match would stop at the first inner <c>&lt;/div&gt;</c> and strand its closing tag in
    /// the message.
    /// </summary>
    [Fact]
    public void A_div_that_merely_contains_the_node_is_not_itself_consumed()
    {
        var wrapped = "<div class=\"wrap\">" + EmailSystemBlockNodes.ActionNodeHtml + "</div>";

        Assert.True(EmailSystemBlockNodes.TrySubstituteActionNode(wrapped, Block, out var result));

        Assert.Equal("<div class=\"wrap\">" + Block + "</div>", result);
    }

    // ── One, and only one ────────────────────────────────────────────────────

    [Fact]
    public void Two_action_areas_are_refused_rather_than_both_rendered()
    {
        var duplicated = EmailSystemBlockNodes.ActionNodeHtml + "<p>x</p>" + EmailSystemBlockNodes.ActionNodeHtml;

        Assert.Equal(2, EmailSystemBlockNodes.CountActionNodes(duplicated));

        var ex = Assert.Throws<BusinessRuleException>(
            () => EmailSystemBlockNodes.AssertAtMostOneActionNode(duplicated));

        Assert.Equal(EmailErrorCodes.ActionBlockMalformed, ex.ErrorCode);
        Assert.Contains("một khối nút phản hồi", ex.Message);
    }

    [Fact]
    public void One_node_and_no_node_are_both_accepted()
    {
        EmailSystemBlockNodes.AssertAtMostOneActionNode(EmailSystemBlockNodes.ActionNodeHtml);
        EmailSystemBlockNodes.AssertAtMostOneActionNode("<p>nothing here</p>");
        EmailSystemBlockNodes.AssertAtMostOneActionNode(null);
    }

    /// <summary>
    /// Substitution replaces exactly one node even if a second slipped past — the block carries a single
    /// one-time token, so two buttons built from it would let the first click answer for both.
    /// </summary>
    [Fact]
    public void Substitution_never_mints_the_same_token_into_two_buttons()
    {
        var duplicated = EmailSystemBlockNodes.ActionNodeHtml + EmailSystemBlockNodes.ActionNodeHtml;

        Assert.True(EmailSystemBlockNodes.TrySubstituteActionNode(duplicated, Block, out var result));

        Assert.Equal(Block + EmailSystemBlockNodes.ActionNodeHtml, result);
    }

    /// <summary>
    /// A block whose markup contains a `$` must not be mangled by regex substitution semantics — real
    /// blocks carry URLs, and a query string is one `$&amp;` away from being rewritten into itself.
    /// </summary>
    [Fact]
    public void A_block_containing_dollar_signs_is_inserted_verbatim()
    {
        const string awkward = "<a href=\"https://x.test/a?b=$1&c=$&\">Đồng ý</a>";

        Assert.True(EmailSystemBlockNodes.TrySubstituteActionNode(
            EmailSystemBlockNodes.ActionNodeHtml, awkward, out var result));

        Assert.Equal(awkward, result);
    }

    // ── The node survives the pipeline it has to travel through ──────────────

    /// <summary>
    /// <c>StripActionArtifacts</c> collapses blocks left holding only whitespace, and the node IS an empty
    /// div. Without its exemption the position would be deleted on the way in and the block would fall
    /// back to being appended — the original defect, restored silently by a cleanup step.
    /// </summary>
    [Fact]
    public void Stripping_action_artifacts_does_not_eat_the_node()
    {
        var authored = "<p>Xin chào,</p>" + EmailSystemBlockNodes.ActionNodeHtml + "<p>Trân trọng,</p>";

        var stripped = EmailComposition.StripActionArtifacts(authored);

        Assert.True(EmailSystemBlockNodes.HasActionNode(stripped));
    }

    [Fact]
    public void An_ordinary_empty_paragraph_is_still_collapsed()
    {
        Assert.DoesNotContain("<p></p>", EmailComposition.StripActionArtifacts("<p>a</p><p></p>"));
    }

    // The matching "…and survives the real sanitiser" case lives in the integration project, which is the
    // one that references Infrastructure: see HiddenEmailContentTests.

    /// <summary>
    /// The node is inert by construction. An author who types one by hand gets an empty div and no
    /// buttons, because the markup is substituted by the backend and never accepted from a client.
    /// </summary>
    [Fact]
    public void The_node_carries_no_link_no_token_and_no_text()
    {
        Assert.DoesNotContain("href", EmailSystemBlockNodes.ActionNodeHtml);
        Assert.DoesNotContain("http", EmailSystemBlockNodes.ActionNodeHtml);
        Assert.DoesNotContain("token", EmailSystemBlockNodes.ActionNodeHtml);
        Assert.Empty(EmailComposition.HtmlToPlainText(EmailSystemBlockNodes.ActionNodeHtml));
    }
}
