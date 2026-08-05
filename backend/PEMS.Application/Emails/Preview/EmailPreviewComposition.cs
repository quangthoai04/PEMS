using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Preview;

/// <summary>
/// Turns a message BODY into the whole message a recipient would open: the action block at the position
/// the body chose for it, inside the branded shell.
///
/// <para>
/// It exists because the two previews were assembled by different code. The final preview ran this logic
/// as a private method of its own handler; the FIRST preview — the one the eye icon opens, and the one
/// most messages are sent from — had no equivalent, so it returned a bare body and left the browser to
/// paste an action block underneath it. The sender therefore approved one shape and the recipient
/// received another, and the only stage that showed the truth was the one reached by editing.
/// </para>
/// <para>
/// The rule that matters is not "share a helper" but "there is ONE answer to what this message looks
/// like". A preview assembled by a second implementation is a drawing of a message, and a drawing cannot
/// be wrong in a way any test would notice.
/// </para>
/// </summary>
public static class EmailPreviewComposition
{
    /// <summary>
    /// Assembles <paramref name="bodyHtml"/> into the message that would be delivered, with the DISABLED
    /// action block — no live URL, no token.
    ///
    /// <para>
    /// The placement mirrors <c>EmailTemplateRenderer.ApplyTrustedBlocks</c>, append fallback included:
    /// a body carrying no node still has to receive its buttons somewhere, and the send appends them.
    /// Refusing here instead would make the preview disagree with the send in exactly the case the
    /// fallback exists for.
    /// </para>
    /// </summary>
    /// <param name="templateCode">Decides which action block, if any, this message carries.</param>
    /// <param name="bodyHtml">The body: rendered template content, or an author's edit of it.</param>
    /// <param name="language">Normalised language, for the block's labels.</param>
    /// <param name="format">
    /// PLAIN_TEXT bodies are returned unshelled. Wrapping one in the HTML card would show the sender a
    /// branded message and deliver a bare one.
    /// </param>
    public static string Assemble(
        string templateCode, string bodyHtml, string language, EmailBodyFormat format)
    {
        var spec = EmailActionTemplates.For(templateCode);

        // Stripped first for the same reason the renderer strips: an author who pasted an action anchor
        // must not end up with two of them. The system node survives this — see StripActionArtifacts.
        var body = EmailComposition.StripActionArtifacts(bodyHtml);

        EmailSystemBlockNodes.AssertAtMostOneActionNode(body);

        if (spec is not null)
        {
            var block = EmailComposition.ActionBlockStart
                        + EmailActionTemplates.DisabledBlockFor(templateCode, language)
                        + EmailComposition.ActionBlockEnd;

            body = EmailSystemBlockNodes.TrySubstituteActionNode(body, block, out var placed)
                ? placed
                : body + block;
        }

        return format == EmailBodyFormat.HTML ? EmailComposition.BrandedShell(body) : body;
    }
}
