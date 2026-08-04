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
    /// The PER-CAMPUS visit this message is about. Supplying it — or a campus/department — switches the
    /// preview from "show an operator what the wording looks like" to "show a sender the message that is
    /// about to go out", and the reply contact is then resolved for real instead of drawn as a stand-in.
    ///
    /// <para>
    /// This is the field whose absence was the defect. Without it the preview had no visit, so it drew the
    /// dashed "hệ thống điền đầu mối…" placeholder INSIDE the body, the host edited that body and sent it
    /// back as authored content, and the dispatcher appended the real contact card underneath — a message
    /// carrying both a stand-in and the thing it was standing in for.
    /// </para>
    /// </summary>
    ulong? VisitInstanceId = null,

    ulong? CampusId = null,

    ulong? DepartmentId = null,

    /// <summary>
    /// A per-message change to the reply contact, so the sender sees exactly what their choice produces
    /// before they commit to it. Re-validated and re-resolved at send time; this preview grants nothing.
    /// </summary>
    PEMS.Application.Emails.Contact.EmailContactOverrideInput? ContactOverride = null)
    : IRequest<PreviewEmailTemplateResponse>
{
    /// <summary>
    /// True when this preview is about a REAL message with a real recipient, rather than a template an
    /// operator is editing.
    ///
    /// <para>
    /// Derived from the context the caller supplied rather than from a flag it sets, because a flag can
    /// disagree with the data. A caller that names a visit is previewing that visit's mail; one that names
    /// nothing has nothing to resolve a Host from, and asking it to also remember to say so would make
    /// "operational preview with the wrong contact" a reachable state.
    /// </para>
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
    /// The reply contact this message will carry, resolved for real — present only for an operational
    /// preview (see <see cref="PreviewEmailTemplateQuery.IsOperational"/>).
    ///
    /// <para>
    /// Its <c>LockedContactBlockHtml</c> is NOT part of <see cref="BodyHtml"/> and must not be merged into
    /// it by the client. The body is what the sender may edit and send back; the block is what the backend
    /// will append, and the two are kept apart so a message can never carry two of them.
    /// </para>
    /// </summary>
    PEMS.Application.Emails.Contact.EmailContactPreviewResult? Contact = null);
