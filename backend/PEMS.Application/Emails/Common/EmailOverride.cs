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
/// <para>
/// <b>There is no contact override on this type any more, and no sender override either.</b> The removed
/// one let a sender name a THIRD PARTY as the message's reply contact, which needed a validator, a
/// candidate service, an authorisation check and an audit row to keep somebody from attributing a
/// hand-written address to a Host. Sender information is not that: it describes the account that pressed
/// send, it is resolved from authentication, and a client has nothing to contribute to it. What the sender
/// MAY change is the wording — including the already-substituted sentence naming them, which by then is
/// ordinary text like any other — and that travels in <see cref="BodyText"/>/<see cref="BodyHtml"/> where
/// it is sanitised with everything else.
/// </para>
/// </summary>
public sealed record EmailOverride(
    bool UseEditedContent,
    string? Subject,
    string? BodyHtml,
    string? BodyText = null,
    System.Collections.Generic.IReadOnlyList<EmailComposeAttachmentInput>? Attachments = null);

public static class EmailOverrideLimits
{
    public const int SubjectMax = 255;
    public const int BodyMax = 50_000;
}
