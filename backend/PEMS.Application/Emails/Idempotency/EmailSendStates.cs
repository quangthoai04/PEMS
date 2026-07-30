namespace PEMS.Application.Emails.Idempotency;

/// <summary>
/// The states a report/invoice send reservation moves through (G11 / R-103).
///
/// <para>
/// The distinction that carries all the weight is between <see cref="FailedBeforeDispatch"/> and
/// <see cref="OutcomeUnknown"/>. Both are failures; only the first is one the system can prove left
/// nothing behind, and therefore only the first may be retried under the same key.
/// </para>
/// </summary>
public static class EmailSendStates
{
    /// <summary>The row was created and this request owns the attempt.</summary>
    public const string Reserved = "RESERVED";

    /// <summary>The handler is running: scope checks, PDF generation, template render, history rows.</summary>
    public const string Preparing = "PREPARING";

    /// <summary>Committed immediately before the outbound call, so a crash here is never read as "not sent".</summary>
    public const string Dispatching = "DISPATCHING";

    /// <summary>The provider accepted the message. Acceptance, not delivery — PEMS has no delivery webhook.</summary>
    public const string Succeeded = "SUCCEEDED";

    /// <summary>
    /// The attempt stopped while it was still provably local: a scope refusal, a broken template, an
    /// unusable PDF, or an email service that never opened a socket. Retryable under the same key.
    /// </summary>
    public const string FailedBeforeDispatch = "FAILED_BEFORE_DISPATCH";

    /// <summary>
    /// The outbound call had started and its result was not established. This is not "failed" — the
    /// message may well have been delivered — and it is never retried automatically. Sending again is a
    /// decision a person makes, with a new key.
    /// </summary>
    public const string OutcomeUnknown = "OUTCOME_UNKNOWN";
}
