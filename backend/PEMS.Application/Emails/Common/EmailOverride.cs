namespace PEMS.Application.Emails.Common;

/// <summary>
/// Optional user-edited email content carried by send/invite commands. When
/// <see cref="UseEditedContent"/> is true the backend uses <see cref="Subject"/> + the edited body as
/// the message content, then ALWAYS injects the system action block with real tokens — the edited body
/// is never trusted to contain live action URLs.
/// <para>
/// Preferred for plain-text edits: <see cref="BodyText"/> — the readable plain text the host edited
/// (the backend converts it to safe HTML). For the rich editor the host sends <see cref="BodyHtml"/>
/// (already cid-rewritten for inline images); <see cref="EmailComposition.ResolveEditableHtml"/> picks
/// BodyText first, falling back to BodyHtml. <see cref="Attachments"/> carries the file/inline-image
/// references (validated + streamed to real MIME parts at send time, same rules as email drafts).
/// </para>
/// </summary>
/// <para>
/// <see cref="ContactOverride"/> travels on the same object rather than as a parameter on each command,
/// for the same reason the subject and body do: the four send paths that open the compose modal are the
/// four that can carry it, and repeating the field on each one is how three of them end up supporting it
/// and the fourth silently does not.
/// </para>
public sealed record EmailOverride(
    bool UseEditedContent,
    string? Subject,
    string? BodyHtml,
    string? BodyText = null,
    System.Collections.Generic.IReadOnlyList<EmailComposeAttachmentInput>? Attachments = null,
    /// <summary>
    /// Who this ONE message tells the recipient to contact. Structured data only — the client never sends
    /// the block's HTML, and the backend never accepts it (see
    /// <c>SystemEmailDispatcher.AssertContactBlockNotSuppliedByCaller</c>).
    ///
    /// <para>
    /// Independent of <see cref="UseEditedContent"/>: a sender may change the contact without touching a
    /// word of the template, and the two are separate decisions with separate audit trails.
    /// </para>
    /// </summary>
    Contact.EmailContactOverrideInput? ContactOverride = null);

public static class EmailOverrideLimits
{
    public const int SubjectMax = 255;
    public const int BodyMax = 50_000;
}
