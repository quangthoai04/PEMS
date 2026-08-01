using System.Collections.Generic;
using PEMS.Domain.Enums;

namespace PEMS.Application.Common.Interfaces;

/// <summary>The two languages <c>email_templates</c> stores content for.</summary>
public static class EmailLanguages
{
    public const string Vi = "VI";
    public const string En = "EN";

    /// <summary>Normalises free-form input to VI/EN, defaulting to Vietnamese.</summary>
    public static string Normalize(string? value)
        => string.Equals(value?.Trim(), En, System.StringComparison.OrdinalIgnoreCase) ? En : Vi;
}

/// <summary>
/// Names of the HTML fragments the backend injects itself. They are referenced from a template body as
/// <c>{{actionBlock}}</c> but are deliberately NOT listed in <c>variables_text</c>: a template author must
/// not be able to supply, move into an editable field, or fabricate the buttons that carry one-time tokens.
/// </summary>
public static class EmailTrustedBlocks
{
    /// <summary>Accept/decline/detail buttons with real (or, in preview, disabled) action links.</summary>
    public const string ActionBlock = "actionBlock";

    /// <summary>The agenda table injected into VISIT_AGENDA_PROPOSAL — Host-typed text, HTML-encoded by
    /// the backend before it is assembled (see <see cref="PEMS.Application.Emails.Common.EmailComposition.AgendaListBlock"/>).</summary>
    public const string AgendaBlock = "agendaBlock";

    /// <summary>Every trusted block name — used by the contract test to exclude them from variable checks.</summary>
    public static readonly IReadOnlyList<string> All = new[] { ActionBlock, AgendaBlock };
}

/// <summary>
/// A request to render one system email from the database template.
/// </summary>
/// <param name="TemplateCode">Must be a registered system template.</param>
/// <param name="Language">VI or EN. No silent fallback to the other language.</param>
/// <param name="Variables">
/// Exactly the variables the template declares — no more, no fewer. Values are treated as untrusted text:
/// they are HTML-encoded before they reach an HTML body.
/// </param>
/// <param name="TrustedHtmlBlocks">
/// Backend-generated HTML injected verbatim (see <see cref="EmailTrustedBlocks"/>). This is the ONLY way
/// raw markup can enter a rendered body.
/// </param>
/// <param name="CampusId">Only for the rare caller that genuinely needs a campus-specific template.</param>
public sealed record EmailRenderRequest(
    string TemplateCode,
    string Language,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyDictionary<string, string>? TrustedHtmlBlocks = null,
    ulong? CampusId = null)
{
    /// <summary>
    /// Where the words come from. Defaults to the template. Authored content still goes through this same
    /// renderer — the template is still resolved, the variable contract still holds, the trusted blocks
    /// are still the backend's, and the subject is still guarded.
    /// </summary>
    public PEMS.Application.Emails.Common.SystemEmailContent Content { get; init; }
        = PEMS.Application.Emails.Common.SystemEmailContent.FromTemplate.Instance;
}

/// <summary>
/// The rendered email. Preview, the actual send, and the <c>sent_emails</c> snapshot all use this same
/// object, so what an operator previews is byte-for-byte what a recipient receives.
/// </summary>
public sealed record EmailRenderResult(
    ulong EmailTemplateId,
    string TemplateCode,
    string Subject,
    string Body,
    EmailBodyFormat BodyFormat,
    string LanguageUsed);

/// <summary>
/// Renders a system email from <c>email_templates</c>.
///
/// <para>
/// <b>There is no content fallback.</b> A missing, inactive, half-translated or mis-declared template
/// fails with a stable error code — it never quietly degrades to a hard-coded subject/body. That is the
/// whole point of making the database the single source of email content: a silent fallback would mean an
/// operator edits a template, sees nothing change, and has no way to find out why.
/// </para>
/// </summary>
public interface IEmailTemplateRenderer
{
    Task<EmailRenderResult> RenderAsync(EmailRenderRequest request, CancellationToken cancellationToken = default);
}
