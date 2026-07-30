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
    bool UseSampleData = false) : IRequest<PreviewEmailTemplateResponse>;

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
    string BodyFormat);
