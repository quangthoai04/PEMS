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

    // ── Send idempotency (G11 / R-103) ───────────────────────────────────────

    /// <summary>
    /// A report/invoice send arrived without an <c>Idempotency-Key</c>. Refused rather than sent: a
    /// keyless path would make the whole guarantee optional, and the caller that forgot the header is
    /// exactly the caller whose retry would send twice.
    /// </summary>
    public const string IdempotencyKeyRequired = "EMAIL_IDEMPOTENCY_KEY_REQUIRED";

    /// <summary>The key is empty, too short, too long, or contains a control character.</summary>
    public const string IdempotencyKeyInvalid = "EMAIL_IDEMPOTENCY_KEY_INVALID";

    /// <summary>Another request with this key is still running. Wait for it; do not send again.</summary>
    public const string IdempotencyInProgress = "EMAIL_IDEMPOTENCY_IN_PROGRESS";

    /// <summary>
    /// This key was already used for a DIFFERENT request. Nothing is sent — the alternative would be to
    /// guess which of the two the user meant.
    /// </summary>
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST";

    /// <summary>
    /// A previous attempt under this key reached the provider and its result was never established. The
    /// message may have been delivered, so it is not retried automatically; sending again is a decision
    /// a person makes, with a new key.
    /// </summary>
    public const string IdempotencyOutcomeUnknown = "EMAIL_IDEMPOTENCY_OUTCOME_UNKNOWN";

    // ── Template editing (G11-J / G11-I) ─────────────────────────────────────
    // These are FIELD-LEVEL codes: each one arrives attached to subjectVi/subjectEn/bodyVi/bodyEn and,
    // where it applies, to the variable at fault. The screen used to render one sentence — "Một số biến
    // chưa được định nghĩa hoặc sai định dạng" — for every one of these situations, which told the
    // operator neither where the problem was nor what to change.

    // An edit that uses a variable the template does not declare reuses the existing
    // TemplateVariableUnknown above: it is the same fault, whether the caller supplied the stray
    // variable at send time or an operator typed it into the body.

    /// <summary>A brace-shaped fragment that is not a well-formed <c>{{camelCase}}</c> placeholder.</summary>
    public const string TemplateVariableMalformed = "EMAIL_TEMPLATE_VARIABLE_MALFORMED";

    /// <summary>The edit removed a variable this template cannot send a usable message without.</summary>
    public const string TemplateRequiredVariableMissing = "EMAIL_TEMPLATE_REQUIRED_VARIABLE_MISSING";

    /// <summary>A placeholder is present but no caller supplies a value for it at send time.</summary>
    public const string TemplateRuntimeVariableMissing = "EMAIL_TEMPLATE_RUNTIME_VARIABLE_MISSING";

    /// <summary>A credential or trusted block was placed in a subject, which is stored and displayed.</summary>
    public const string TemplateSubjectForbiddenSensitiveVariable =
        "EMAIL_TEMPLATE_SUBJECT_FORBIDDEN_SENSITIVE_VARIABLE";

    /// <summary>The edit removed <c>{{actionBlock}}</c> from a template whose message is the action.</summary>
    public const string TemplateActionBlockRequired = "EMAIL_TEMPLATE_ACTION_BLOCK_REQUIRED";

    /// <summary>
    /// The stored body of a template whose content IS a trusted block no longer contains that block's
    /// placeholder, so the block the caller built had nowhere to go.
    ///
    /// <para>
    /// Its own code because the failure is a data defect with a known repair — the database row has
    /// fallen behind the canonical template and must be re-synced — and not a fault in the sending code
    /// or the caller's variables. Without it this went out silently: substitution has nothing to replace,
    /// the unresolved-placeholder guard sees no leftover braces, and the recipient gets a covering
    /// sentence announcing an update that is not in the mail.
    /// </para>
    /// </summary>
    public const string TemplateRequiredBlockNotInBody = "EMAIL_TEMPLATE_REQUIRED_BLOCK_NOT_IN_BODY";

    /// <summary>
    /// A trusted block was written into a template that has no such block to inject — e.g.
    /// <c>{{setupSummaryBlock}}</c> pasted into an account notice.
    ///
    /// <para>
    /// Its own code because the previous answer was <see cref="TemplateVariableUnknown"/>, which says
    /// "biến không tồn tại trong hệ thống" about a placeholder that certainly does exist and is required
    /// elsewhere. That named the wrong thing and implied the wrong repair (define the variable, or wait
    /// for a catalog fix) when the actual repair is to delete the block from this template's body. A
    /// block is refused at SAVE, where the operator can still see what they pasted, rather than at send
    /// time where nothing would be substituted for it.
    /// </para>
    /// </summary>
    public const string TemplateSystemBlockNotAllowed = "EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED";

    /// <summary>
    /// A create, delete, clone or status change was attempted against the system template catalog. The
    /// catalog is fixed in code: a template code exists because a caller in a release sends it, so one
    /// invented through the API would be a row nothing can ever address.
    /// </summary>
    public const string TemplateCatalogFixed = "EMAIL_TEMPLATE_CATALOG_FIXED";

    /// <summary>
    /// An update tried to change a field that is not the operator's to change — the code, the module, the
    /// security classification, the status or the variable contract.
    /// </summary>
    public const string TemplateFieldImmutable = "EMAIL_TEMPLATE_FIELD_IMMUTABLE";

    /// <summary>
    /// The template was modified by somebody else after this editor loaded it. Refused rather than
    /// last-write-wins: two operators editing the same message would otherwise silently discard one
    /// person's wording with no trace that it ever existed.
    /// </summary>
    public const string TemplateConcurrencyConflict = "EMAIL_TEMPLATE_CONCURRENCY_CONFLICT";

    /// <summary>The template code has no shipped default recorded, so there is nothing to restore to.</summary>
    public const string TemplateDefaultUnavailable = "EMAIL_TEMPLATE_DEFAULT_UNAVAILABLE";

    // ── Reply-contact block ──────────────────────────────────────────────────

    /// <summary>
    /// A template whose text instructs the recipient to contact somebody could not resolve anybody to
    /// contact, and had no fallback left.
    ///
    /// <para>
    /// Fail-closed rather than send-without: the alternative is a message that says "please contact the
    /// host" and shows no address, which is the defect the block exists to remove. Only REQUIRED
    /// templates reach this — an OPTIONAL one simply renders nothing.
    /// </para>
    /// </summary>
    public const string ContactRequiredButNotFound = "EMAIL_CONTACT_REQUIRED_BUT_NOT_FOUND";

    /// <summary>
    /// The stored body of a template whose policy is REQUIRED no longer contains
    /// <c>{{contactInformationBlock}}</c>, so the resolved contact would have nowhere to go.
    ///
    /// <para>
    /// The same shape of fault as <see cref="TemplateRequiredBlockNotInBody"/> and separated from it for
    /// the same reason: this one names a policy an operator can change, not a row to re-sync.
    /// </para>
    /// </summary>
    public const string TemplateRequiredContactBlockNotInBody =
        "EMAIL_TEMPLATE_REQUIRED_CONTACT_BLOCK_NOT_IN_BODY";

    /// <summary>
    /// The resolved policy contradicts itself — e.g. REQUIRED with both email and phone hidden, which
    /// would render a heading and a name with no way to reach it. Refused when the policy is resolved,
    /// not when a recipient notices.
    /// </summary>
    public const string ContactConfigurationInvalid = "EMAIL_CONTACT_CONFIGURATION_INVALID";

    /// <summary>
    /// The address a Reply-To policy produced is not a usable mailbox. Refused rather than dropped:
    /// silently sending replies to the system mailbox when the policy promised the Host is the same quiet
    /// lie as a contact line with no address.
    /// </summary>
    public const string ReplyToInvalid = "EMAIL_REPLY_TO_INVALID";

    /// <summary>
    /// The contact-policy store could not be read at all — <c>email_contact_policies</c> is missing from
    /// this database, or the query against it failed.
    ///
    /// <para>
    /// Distinct from every "not found" above, and that distinction is the point. A database that has not
    /// had <c>2026-08-03_email_contact_information_block.sql</c> applied answers the settings screen with
    /// a failure that is indistinguishable, from the client, from a mistyped template code or a route
    /// that does not exist — all three arrived as one generic sentence, and the operator was left to
    /// guess which of "run the patch", "fix the code" or "restart the API" they needed. This code names
    /// the first of those three so the screen can say it.
    /// </para>
    /// </summary>
    public const string ContactPolicyStoreUnavailable = "EMAIL_CONTACT_POLICY_STORE_UNAVAILABLE";
}
