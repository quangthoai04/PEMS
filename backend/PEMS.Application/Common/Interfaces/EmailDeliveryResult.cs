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
