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

    /// <summary>The SMTP client threw. What the provider did with the message is NOT known.</summary>
    public const string SmtpSendFailed = "SMTP_SEND_FAILED";

    /// <summary>
    /// True only for outcomes decided before the provider was contacted. Deliberately a closed list:
    /// a code nobody has classified reads as "unknown", which is the safe direction to be wrong in.
    /// </summary>
    public static bool ProvesNothingWasSent(EmailDeliveryResult delivery)
        => delivery.Status == EmailDeliveryStatus.Skipped
           || delivery.Code is SmtpDisabled or SmtpMisconfigured;
}
