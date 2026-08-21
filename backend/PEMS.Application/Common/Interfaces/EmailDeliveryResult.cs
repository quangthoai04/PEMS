namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// The truthful outcome of an email delivery attempt. The system must never record "sent" unless the
/// provider actually accepted the message.
/// </summary>
public enum EmailDeliveryStatus
{
    /// <summary>The provider accepted the message for delivery.</summary>
    Sent,

    /// <summary>
    /// Intentionally NOT sent (e.g. SMTP disabled in a non-production environment). This is neither a
    /// success nor a failure — it must never be recorded as "sent", and it must not roll back business data.
    /// </summary>
    Skipped,

    /// <summary>
    /// Delivery was required but did not succeed — a provider error, or fail-closed because SMTP is a
    /// required feature but is disabled/misconfigured in Production.
    /// </summary>
    Failed,
}

/// <summary>
/// Result of attempting to deliver an email. Carries only NON-secret metadata — a machine
/// <see cref="Code"/>, a human-safe <see cref="SafeMessage"/> and an optional provider id. It never
/// contains the OTP, action token, confirmation URL, body or any other secret.
/// </summary>
public sealed record EmailDeliveryResult(
    EmailDeliveryStatus Status,
    string? Code = null,
    string? SafeMessage = null,
    string? ProviderMessageId = null)
{
    public bool IsSent => Status == EmailDeliveryStatus.Sent;

    public static EmailDeliveryResult Sent(string? providerMessageId = null)
        => new(EmailDeliveryStatus.Sent, ProviderMessageId: providerMessageId);

    public static EmailDeliveryResult Skipped(string code, string? safeMessage = null)
        => new(EmailDeliveryStatus.Skipped, code, safeMessage);

    public static EmailDeliveryResult Failed(string code, string safeMessage)
        => new(EmailDeliveryStatus.Failed, code, safeMessage);
}

/// <summary>
/// The <see cref="EmailDeliveryResult.Code"/> values the email service produces, and the one question
/// that has to be asked of them: <b>can the system prove nothing left the process?</b>
///
/// <para>
/// Send idempotency turns on that distinction (G11 / R-103). A configuration refusal happens before any
/// socket is opened, so a retry is provably safe. An exception from the SMTP client is not the same
/// thing at all — it cannot tell "the server refused the message" apart from "the server accepted it and
/// the acknowledgement was lost" — so it must never be reported as a clean failure.
/// </para>
/// </summary>
public static class EmailDeliveryCodes
{
    /// <summary>SMTP is switched off for this environment. Nothing was attempted.</summary>
    public const string SmtpDisabled = "SMTP_DISABLED";

    /// <summary>No host and no pickup directory. Nothing was attempted.</summary>
    public const string SmtpMisconfigured = "SMTP_MISCONFIGURED";

    /// <summary>
    /// The SMTP client threw and no more specific classification applied. Kept as the last-resort
    /// fallback (see <see cref="PEMS.Infrastructure.Email.SmtpDeliveryClassifier"/>) — no longer the
    /// default outcome for every SMTP exception the way it was before granular classification.
    /// </summary>
    public const string SmtpSendFailed = "SMTP_SEND_FAILED";

    // ── Granular SMTP outcomes (mirrors the RESEND_* codes' granularity) ──────────────────────────
    // All produced by SmtpDeliveryClassifier, never inferred here. None of these prove the whole
    // envelope (TO+CC+BCC, one SMTP transaction) was never submitted UNLESS explicitly noted below —
    // see ProvesNothingWasSent's closed list, which is deliberately conservative about which of these
    // qualify.

    /// <summary>Explicit authentication-specific evidence (typed exception, or a message naming
    /// credentials/authentication) — never inferred from a bare status code alone.</summary>
    public const string SmtpAuthFailed = "SMTP_AUTH_FAILED";

    /// <summary>The server named a specific rejected recipient (<c>SmtpFailedRecipient(s)Exception</c>).
    /// Message-level AMBIGUOUS: other recipients in the same TO+CC+BCC envelope may already have been
    /// accepted, so this is not proof the whole send produced no side effect.</summary>
    public const string SmtpRecipientRejected = "SMTP_RECIPIENT_REJECTED";

    /// <summary>Explicit provider throttling/rate-limit evidence.</summary>
    public const string SmtpRateLimited = "SMTP_RATE_LIMITED";

    /// <summary>Explicit sending-quota/daily-limit evidence — distinct from a short-term rate limit.</summary>
    public const string SmtpQuotaExceeded = "SMTP_QUOTA_EXCEEDED";

    /// <summary>A generic temporary/4xx-shaped rejection (mailbox busy, insufficient storage) with no
    /// explicit rate or quota evidence either way.</summary>
    public const string SmtpTemporaryRejected = "SMTP_TEMPORARY_REJECTED";

    /// <summary>TLS/STARTTLS negotiation failed.</summary>
    public const string SmtpTlsFailed = "SMTP_TLS_FAILED";

    /// <summary>The socket/connection itself failed (refused, DNS, unreachable).</summary>
    public const string SmtpConnectionFailed = "SMTP_CONNECTION_FAILED";

    /// <summary>A genuine (non-caller-cancelled) timeout.</summary>
    public const string SmtpTimeout = "SMTP_TIMEOUT";

    /// <summary>A well-formed SMTP rejection that fits no more specific category above.</summary>
    public const string SmtpProviderRejected = "SMTP_PROVIDER_REJECTED";

    /// <summary>An exception shape the classifier does not recognize — never proof of anything.</summary>
    public const string SmtpNetworkUnknown = "SMTP_NETWORK_UNKNOWN";

    /// <summary>Resend is off or has no API key. The HTTP call was never made.</summary>
    public const string ResendMisconfigured = "RESEND_MISCONFIGURED";

    /// <summary>The stored Resend key could not be decrypted. The HTTP call was never made.</summary>
    public const string ResendCredentialError = "RESEND_CREDENTIAL_ERROR";

    /// <summary>
    /// Superseded by the granular <c>RESEND_*</c> codes in
    /// <see cref="PEMS.Application.ApiIntegrations.Common.ResendDeliveryClassifier"/>
    /// (rate limit, quota, auth, sender, request-invalid, provider-rejected, server-error,
    /// network-unknown) — a Resend rejection and a dead socket used to share this one code, which made
    /// every Resend failure look alike to anything reading <c>sent_emails</c> later. Kept only so
    /// <see cref="EmailAttemptRecord.Classify"/> still reads pre-existing rows correctly; nothing produces
    /// it anymore.
    /// </summary>
    [Obsolete("Replaced by the granular RESEND_* codes in ResendDeliveryClassifier. Kept to classify pre-existing sent_emails rows only.")]
    public const string ResendSendFailed = "RESEND_SEND_FAILED";

    /// <summary>
    /// True only for outcomes decided before the provider was contacted. Deliberately a closed list:
    /// a code nobody has classified reads as "unknown", which is the safe direction to be wrong in.
    ///
    /// <para>
    /// <see cref="PEMS.Application.ApiIntegrations.Common.ResendDeliveryCodes.NetworkUnknown"/> is
    /// deliberately NOT in this list — a transport exception never proves the request did not reach
    /// Resend. Nor are the Resend rejection codes (rate limit, quota, auth, sender, request-invalid,
    /// provider-rejected, server-error): the HTTP request WAS sent, so whatever Resend decided with it is
    /// not "nothing was sent" — it is a real outcome, just not <see cref="EmailDeliveryStatus.Sent"/>.
    /// </para>
    /// </summary>
    public static bool ProvesNothingWasSent(EmailDeliveryResult delivery)
        => delivery.Status == EmailDeliveryStatus.Skipped
           || ProvesNothingWasSent(delivery.Code);

    /// <summary>The same closed list, asked of a code recovered from a stored row.</summary>
    public static bool ProvesNothingWasSent(string? code)
        => code is SmtpDisabled or SmtpMisconfigured
                or ResendMisconfigured or ResendCredentialError;
}

/// <summary>
/// What a stored <c>sent_emails</c> row proves about whether its message reached a provider — the
/// question a retry has to answer once the <see cref="EmailDeliveryResult"/> itself is long gone.
///
/// <para>
/// The in-memory <see cref="EmailDeliveryCodes.ProvesNothingWasSent(EmailDeliveryResult)"/> answers it
/// for the caller that is still holding the result. A recovery sweep running an hour or a week later
/// holds only the row, so the machine code has to survive INTO that row — which is why
/// <see cref="Format"/> writes it there and <see cref="CodeOf"/> reads it back.
/// </para>
/// </summary>
public enum EmailAttemptOutcome
{
    /// <summary>The provider accepted it. Complete — never retry (plan §40 class C).</summary>
    Accepted,

    /// <summary>
    /// Decided before anything was dispatched: SMTP off, no host, no API key. Retrying is provably
    /// safe because there is provably no first copy (plan §40 classes A and B).
    /// </summary>
    ProvenNotDispatched,

    /// <summary>
    /// The outbound call may have reached the provider and its acceptance cannot be established — a
    /// client exception, or a row still QUEUED because the process died between writing it and
    /// recording the outcome. The recipient may already hold the message, so an automatic resend is
    /// NOT allowed (plan §40 class D, §42).
    /// </summary>
    Unknown,
}

/// <summary>
/// Reads and writes the delivery outcome of a stored <c>sent_emails</c> row.
///
/// <para>
/// The row already carried the human-safe message; what it did not carry was the machine code that
/// says whether a retry is safe, so every failure looked alike to anything reading the table later.
/// The code is non-secret by the contract on <see cref="EmailDeliveryResult"/> — it is the one part of
/// the outcome that is safe to persist verbatim — so it is written as a bracketed prefix and parsed
/// back off the front. A row with no prefix (written before this, or by something else) yields a null
/// code and therefore classifies as <see cref="EmailAttemptOutcome.Unknown"/>: the safe direction.
/// </para>
/// </summary>
public static class EmailAttemptRecord
{
    /// <summary>The <c>sent_emails.status</c> value meaning the provider accepted the message.</summary>
    public const string SentStatus = "SENT";

    /// <summary>The <c>sent_emails.status</c> value a row is written with before dispatch.</summary>
    public const string QueuedStatus = "QUEUED";

    /// <summary>The <c>sent_emails.status</c> value for an attempt that did not succeed.</summary>
    public const string FailedStatus = "FAILED";

    /// <summary>Renders the outcome for <c>sent_emails.error_message</c>, keeping the machine code.</summary>
    public static string? Format(EmailDeliveryResult delivery)
        => string.IsNullOrWhiteSpace(delivery.Code)
            ? delivery.SafeMessage
            : $"[{delivery.Code}] {delivery.SafeMessage}".TrimEnd();

    /// <summary>The machine code a stored message was written with, or null when it carries none.</summary>
    public static string? CodeOf(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage) || errorMessage[0] != '[') return null;
        var end = errorMessage.IndexOf(']');
        return end > 1 ? errorMessage[1..end] : null;
    }

    /// <summary>
    /// Classifies one stored attempt.
    ///
    /// <para>
    /// QUEUED is the interesting case, because two very different things leave it there. A SKIPPED send
    /// records its reason, so a QUEUED row WITH a message is an environment that never dispatches mail.
    /// A QUEUED row with NO message is a row that was written and then never spoken of again — the
    /// crash window of plan §42 — and the message may well have gone out.
    /// </para>
    /// </summary>
    public static EmailAttemptOutcome Classify(string? status, string? errorMessage)
    {
        if (status == SentStatus) return EmailAttemptOutcome.Accepted;

        var code = CodeOf(errorMessage);
        if (EmailDeliveryCodes.ProvesNothingWasSent(code)) return EmailAttemptOutcome.ProvenNotDispatched;

        return EmailAttemptOutcome.Unknown;
    }
}
