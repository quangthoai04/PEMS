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
    string? Language) : IRequest<PreviewEmailTemplateResponse>;

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
    bool Editable);
