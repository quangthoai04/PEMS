namespace PEMS.Application.Emails.Common;

/// <summary>
/// Stable, machine-readable failure codes for the email pipeline.
///
/// The whole point of standardising on <c>email_templates</c> is that content lives in the database and
/// the code has NO business-content fallback. That is only safe if every way a render/send can fail
/// surfaces as a code the caller and the operator can act on — never as a silent substitution and never
/// as an opaque 500. Each code below has at least one negative test.
///
/// Thrown via the existing exception convention (see the mapping in
/// <c>docs/email-standardization/02-decisions-and-contracts.md</c> §C-05), not a new hierarchy.
/// </summary>
public static class EmailErrorCodes
{
    // ── Template resolution / rendering ──────────────────────────────────────

    /// <summary>Template code is not in the registry, or has no row in <c>email_templates</c>.</summary>
    public const string TemplateNotFound = "EMAIL_TEMPLATE_NOT_FOUND";

    /// <summary>The row exists but <c>status != 'ACTIVE'</c>.</summary>
    public const string TemplateInactive = "EMAIL_TEMPLATE_INACTIVE";

    /// <summary>
    /// The requested language has no subject or no body. The renderer does NOT quietly fall back to the
    /// other language: a half-translated template is a content bug an operator must see.
    /// </summary>
    public const string TemplateLanguageContentMissing = "EMAIL_TEMPLATE_LANGUAGE_CONTENT_MISSING";

    /// <summary>The caller did not supply a variable the template declares in <c>variables_text</c>.</summary>
    public const string TemplateVariableMissing = "EMAIL_TEMPLATE_VARIABLE_MISSING";

    /// <summary>The caller supplied a variable the template does not declare (usually a rename drift).</summary>
    public const string TemplateVariableUnknown = "EMAIL_TEMPLATE_VARIABLE_UNKNOWN";

    /// <summary>A <c>{{placeholder}}</c> survived rendering — the recipient must never see one.</summary>
    public const string TemplateUnresolvedPlaceholder = "EMAIL_TEMPLATE_UNRESOLVED_PLACEHOLDER";

    /// <summary>Template content failed sanitisation on create/update (e.g. sanitises down to nothing).</summary>
    public const string TemplateContentInvalid = "EMAIL_TEMPLATE_CONTENT_INVALID";

    /// <summary>
    /// A subject interpolates a credential — an OTP variable or a trusted block holding a one-time link.
    /// Subjects ARE stored in the email history, so this is refused before anything is sent or recorded.
    /// </summary>
    public const string TemplateSensitiveInSubject = "EMAIL_TEMPLATE_SENSITIVE_IN_SUBJECT";

    // ── Authored content (a person edited the message before sending) ────────

    /// <summary>Authored mode was chosen with no subject.</summary>
    public const string AuthoredSubjectRequired = "EMAIL_AUTHORED_SUBJECT_REQUIRED";

    /// <summary>Authored subject exceeds the column/header limit.</summary>
    public const string AuthoredSubjectTooLong = "EMAIL_AUTHORED_SUBJECT_TOO_LONG";

    /// <summary>Authored mode was chosen with an empty body, or one that sanitises down to nothing.</summary>
    public const string AuthoredBodyRequired = "EMAIL_AUTHORED_BODY_REQUIRED";

    /// <summary>Authored body exceeds the stored-body limit.</summary>
    public const string AuthoredBodyTooLong = "EMAIL_AUTHORED_BODY_TOO_LONG";

    /// <summary>
    /// The author tried to supply the action block themselves — either the canonical markers or a
    /// <c>{{actionBlock}}</c> placeholder. The block is minted by the backend from a real token; an
    /// author-placed one would either duplicate it or move the boundary the history strip depends on.
    /// </summary>
    public const string AuthoredActionBlockForbidden = "EMAIL_AUTHORED_ACTION_BLOCK_FORBIDDEN";

    // ── Action-block integrity ───────────────────────────────────────────────

    /// <summary>
    /// Action-block markers that do not form exactly one well-ordered block: an unclosed START, a stray
    /// END, nesting, or several blocks in one message. Refused rather than repaired, because every one of
    /// those shapes makes "everything between the markers is removed from the history" untrue.
    /// </summary>
    public const string ActionBlockMalformed = "EMAIL_ACTION_BLOCK_MALFORMED";

    /// <summary>
    /// A rendered subject literally contains a secret VALUE — an OTP, or a one-time URL from the action
    /// block — rather than a placeholder for one. Distinct from
    /// <see cref="TemplateSensitiveInSubject"/>, which catches the declared-placeholder form before
    /// substitution; this catches a value pasted in as text. The error names neither the value nor the URL.
    /// </summary>
    public const string SubjectSecretLeak = "EMAIL_SUBJECT_SECRET_LEAK";

    /// <summary>
    /// The body about to be written to <c>sent_emails.body_snapshot</c> still contains a one-time URL
    /// after the retention policy was applied. A last-line invariant, not an expected outcome: the
    /// history API serves that column to every internal role, so a stored link is a shared credential.
    /// </summary>
    public const string HistorySecretLeak = "EMAIL_HISTORY_SECRET_LEAK";

    // ── Recipients ───────────────────────────────────────────────────────────

    /// <summary>No TO recipient. CC/BCC alone is never a valid envelope.</summary>
    public const string RecipientRequired = "EMAIL_RECIPIENT_REQUIRED";

    /// <summary>Address is not a parseable mailbox.</summary>
    public const string RecipientInvalid = "EMAIL_RECIPIENT_INVALID";

    /// <summary>Same address twice within one group (case-insensitive).</summary>
    public const string RecipientDuplicate = "EMAIL_RECIPIENT_DUPLICATE";

    /// <summary>
    /// Same address in two groups — e.g. TO and BCC. Left unchecked this leaks BCC membership, because
    /// the address is visible in the TO header while also being counted as a blind copy.
    /// </summary>
    public const string RecipientCrossGroupDuplicate = "EMAIL_RECIPIENT_CROSS_GROUP_DUPLICATE";

    /// <summary>Total recipients exceed the configured ceiling.</summary>
    public const string RecipientLimitExceeded = "EMAIL_RECIPIENT_LIMIT_EXCEEDED";

    /// <summary>The template's policy forbids the recipient type used (e.g. CC on a one-time-token email).</summary>
    public const string RecipientTypeNotAllowed = "EMAIL_RECIPIENT_TYPE_NOT_ALLOWED";

    // ── Headers ──────────────────────────────────────────────────────────────

    /// <summary>CR/LF or a control character in a subject, address or display name (header injection).</summary>
    public const string HeaderInvalid = "EMAIL_HEADER_INVALID";

    // ── Report attachments ───────────────────────────────────────────────────

    /// <summary>
    /// The attachment file name is not safe to put in a <c>Content-Disposition</c> header — a control
    /// character, a path separator or a parent-directory hop. Refused, never rewritten.
    /// </summary>
    public const string ReportAttachmentNameInvalid = "EMAIL_REPORT_ATTACHMENT_NAME_INVALID";

    /// <summary>
    /// The generated report is empty or is not a PDF. The four REPORT templates all say "đính kèm là…",
    /// so a message without a readable document would be telling the recipient something untrue.
    /// </summary>
    public const string ReportAttachmentInvalid = "EMAIL_REPORT_ATTACHMENT_INVALID";

    /// <summary>The report PDF could not be written to file storage, so there is nothing to attach.</summary>
    public const string ReportAttachmentStorageFailed = "EMAIL_REPORT_ATTACHMENT_STORAGE_FAILED";

    /// <summary>
    /// A report/invoice send did not reach the provider. These commands are Mandatory: the user pressed
    /// "gửi", so a Skipped or Failed delivery must surface as a failed command, never as a quiet success.
    /// </summary>
    public const string ReportDeliveryFailed = "EMAIL_REPORT_DELIVERY_FAILED";
}
