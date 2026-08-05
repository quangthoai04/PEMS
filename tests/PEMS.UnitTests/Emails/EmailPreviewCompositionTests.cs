using System;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Preview;
using PEMS.Domain.Enums;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The composer both previews run through.
///
/// <para>
/// It was extracted from the final preview's handler because the FIRST preview had no equivalent: the
/// eye icon returned a bare body and the browser assembled a read-only view of its own. Two assemblies
/// of the same message is a defect that can only be caught by comparing screenshots — so there is now
/// one, and these are its rules.
/// </para>
/// </summary>
public sealed class EmailPreviewCompositionTests
{
    /// <summary>An action template — its registry entry decides which buttons the composer attaches.</summary>
    private const string ActionTemplate = SystemEmailTemplates.VisitParticipantInvitation;

    private static string Assemble(string body, EmailBodyFormat format = EmailBodyFormat.HTML)
        => EmailPreviewComposition.Assemble(ActionTemplate, body, EmailLanguages.Vi, format);

    [Fact]
    public void An_html_message_is_wrapped_in_the_branded_shell()
    {
        var html = Assemble("<p>Xin chào</p>");

        Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PEMS — Campus Visit", html, StringComparison.Ordinal);
        Assert.Contains("Xin chào", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A plain-text template is NOT shelled.
    ///
    /// <para>
    /// Wrapping one would show the sender a branded card and deliver a bare message — a preview that is
    /// wrong in the direction nobody checks, because it looks better than the truth.
    /// </para>
    /// </summary>
    [Fact]
    public void A_plain_text_message_is_left_unshelled()
    {
        var text = Assemble("Xin chào", EmailBodyFormat.PLAIN_TEXT);

        Assert.DoesNotContain("<!DOCTYPE", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Xin chào", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_action_block_replaces_the_node_at_its_position()
    {
        var html = Assemble(
            "<p>TRUOC</p>" + EmailSystemBlockNodes.ActionNodeHtml + "<p>SAU</p>");

        var before = html.IndexOf("TRUOC", StringComparison.Ordinal);
        var block = html.IndexOf(EmailComposition.ActionBlockStart, StringComparison.Ordinal);
        var after = html.IndexOf("SAU", StringComparison.Ordinal);

        Assert.True(before >= 0 && block >= 0 && after >= 0);
        Assert.True(before < block && block < after,
            $"the block must land where the node was. before={before}, block={block}, after={after}");

        // The node itself is consumed, not left behind next to the block it was replaced by.
        Assert.False(EmailSystemBlockNodes.HasActionNode(html));
    }

    /// <summary>
    /// A body with no node still gets its buttons, appended.
    ///
    /// <para>
    /// Kept deliberately, and pinned because a regression would be silent: content composed before the
    /// node existed would go out with no action area at all, and the recipient would simply have nothing
    /// to press. The send has the same fallback, so removing it here would ALSO make the preview
    /// disagree with the send in exactly the case the fallback exists for.
    /// </para>
    /// </summary>
    [Fact]
    public void A_body_with_no_node_still_receives_its_action_block()
    {
        var html = Assemble("<p>CHI CO CHU</p>");

        Assert.Contains(EmailComposition.ActionBlockStart, html, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf(EmailComposition.ActionBlockStart, StringComparison.Ordinal)
            > html.IndexOf("CHI CO CHU", StringComparison.Ordinal),
            "with no node to place it, the block belongs after the text");
    }

    /// <summary>
    /// The buttons are inert: spans, no href, no token.
    ///
    /// <para>
    /// This is the one difference from a real send that the composer is allowed to have, and the reason
    /// it is allowed: a preview that minted a live credential would let a sender answer their own
    /// message by mis-clicking a picture of it — and would put a working accept link in anything that
    /// logs or forwards a preview.
    /// </para>
    /// </summary>
    [Fact]
    public void The_preview_buttons_carry_no_link_and_no_token()
    {
        var html = Assemble("<p>Xin chào</p>" + EmailSystemBlockNodes.ActionNodeHtml);

        Assert.DoesNotContain("<a ", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/public/email-actions/", html, StringComparison.OrdinalIgnoreCase);

        // …but they still say what they do. A preview of buttons nobody can read is not a preview.
        Assert.Contains("Chấp nhận", html, StringComparison.Ordinal);
        Assert.Contains("Từ chối", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A template with no action registry entry gets no block invented for it.
    /// </summary>
    [Fact]
    public void A_plain_template_receives_no_action_block()
    {
        var html = EmailPreviewComposition.Assemble(
            SystemEmailTemplates.AccountEmailConfirmation + "-NOT-A-REAL-CODE",
            "<p>Xin chào</p>",
            EmailLanguages.Vi,
            EmailBodyFormat.HTML);

        Assert.DoesNotContain(EmailComposition.ActionBlockStart, html, StringComparison.Ordinal);
        Assert.Contains("Xin chào", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two action nodes are refused rather than silently collapsed to one.
    ///
    /// <para>
    /// An author who duplicates the node is asking for two sets of buttons, and the recipient would get
    /// one set plus a hole — or two live credentials for the same decision. Both previews and the send
    /// refuse it, so the author finds out while the message is still in front of them.
    /// </para>
    /// </summary>
    [Fact]
    public void Two_action_nodes_are_refused()
    {
        var body = "<p>A</p>" + EmailSystemBlockNodes.ActionNodeHtml
                   + "<p>B</p>" + EmailSystemBlockNodes.ActionNodeHtml;

        Assert.ThrowsAny<Exception>(() => Assemble(body));
    }
}
