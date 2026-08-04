using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Emails.Queries.PreviewEmailTemplate;

/// <summary>
/// Renders an email template's subject/body from email_templates with the supplied variable context,
/// for an editable "Xem trước email" modal. Read-only: never inserts sent_emails /
/// sent_email_recipients / email_action_tokens and never sends SMTP. For action templates the action
/// buttons are NOT rendered with live tokens — the response carries a read-only (disabled) action
/// block so the user cannot edit/break them.
/// </summary>
public sealed record PreviewEmailTemplateQuery(
    string TemplateCode,
    Dictionary<string, string>? Context,
    string? Language,
    /// <summary>
    /// Fill any variable the caller did not supply with the backend contract's sample value (G11-J).
    ///
    /// <para>
    /// Two callers, two different needs, and they cannot share one behaviour. The TEMPLATE-MANAGEMENT
    /// screen has no real message in hand — it is showing an operator what the wording looks like — so
    /// it needs samples, and without them a canonical template previews as an error because nothing
    /// supplies its variables. The COMPOSE modal is previewing a REAL message about to be sent to a
    /// real person; if a caller forgets a variable there, quietly substituting "Nguyễn Văn An" would
    /// show the host a preview that differs from what the recipient receives, and they would approve it.
    /// </para>
    /// <para>
    /// Default false: strict, matching the send. A caller opts in to samples, and opting in is only
    /// correct when there is no real data to be wrong about.
    /// </para>
    /// </summary>
    bool UseSampleData = false,

    /// <summary>
    /// The PER-CAMPUS visit this message is about, when there is one.
    ///
    /// <para>
    /// It no longer changes what the preview RENDERS — the sender is resolved from the signed-in account
    /// either way — but it is still the thing that distinguishes "an operator is reading a template" from
    /// "somebody is about to send this message", which the prepared-preview pipeline binds into its token.
    /// </para>
    /// </summary>
    ulong? VisitInstanceId = null,

    ulong? CampusId = null,

    ulong? DepartmentId = null,

    /// <summary>
    /// What this message is about, in the form the SENDING command will recompute from its own arguments
    /// — e.g. <c>visitInstance:41|participant:907</c>.
    ///
    /// <para>
    /// Taken from the client and trusted for nothing. It is signed into the preview token, and the send
    /// recomputes it from the ids it was itself given; a client that puts the wrong value here gets a
    /// token every send refuses. What it buys is that one preview endpoint serves every flow without
    /// growing a branch per flow.
    /// </para>
    /// </summary>
    string? ScopeKey = null)
    : IRequest<PreviewEmailTemplateResponse>
{
    /// <summary>
    /// True when this preview is about a REAL message with a real recipient, rather than a template an
    /// operator is editing. Derived from the context the caller supplied rather than from a flag it sets,
    /// because a flag can disagree with the data.
    /// </summary>
    public bool IsOperational =>
        VisitInstanceId is not null || CampusId is not null || DepartmentId is not null;
}

public sealed record PreviewEmailTemplateResponse(
    string TemplateCode,
    string Subject,
    /// <summary>Editable message content as HTML (action buttons stripped for action templates). Kept
    /// for the read-only rendered preview; the editor binds to <see cref="EditableBodyText"/>.</summary>
    string BodyHtml,
    /// <summary>The same editable content as readable plain text (no &lt;p&gt;/&lt;br&gt; tags) — what
    /// the host edits in the modal. Sent back as emailOverride.bodyText.</summary>
    string EditableBodyText,
    bool IsActionTemplate,
    string? SystemActionDescription,
    /// <summary>Read-only (disabled) preview of the system action block, if any.</summary>
    string? LockedActionBlockHtml,
    string[] RequiredActionPlaceholders,
    bool Editable,
    /// <summary>Body format of the source template: "PLAIN_TEXT" | "HTML" (from email_templates.body_format).</summary>
    string BodyFormat,
    /// <summary>
    /// Where a reply to this message would go — the sender's own address, or the system support address
    /// for mail nobody presses send on.
    ///
    /// <para>
    /// Reported as its own field rather than inferred from the body. The preview says "khi người nhận bấm
    /// Trả lời, email sẽ gửi tới …", and reading that out of the rendered HTML would be guessing at
    /// something the send decides from the account — the two could disagree and the reader would have no
    /// way to tell which was true.
    /// </para>
    /// </summary>
    string? ReplyToEmail = null,
    /// <summary>True when the send flow may offer a "Chỉnh sửa" button for this template.</summary>
    bool RuntimeEditable = false,
    /// <summary>
    /// Signed proof of what this preview rendered, to be handed to <c>final-preview</c> when the sender
    /// edits. Null when the template is not runtime-editable — there is nothing to hand anywhere.
    /// </summary>
    string? PreviewToken = null,
    /// <summary>When <see cref="PreviewToken"/> stops being accepted. ISO-8601.</summary>
    string? ExpiresAt = null);
