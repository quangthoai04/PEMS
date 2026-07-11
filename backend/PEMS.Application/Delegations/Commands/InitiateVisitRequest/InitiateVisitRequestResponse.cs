namespace PEMS.Application.Delegations.Commands.InitiateVisitRequest;

/// <summary>
/// Returned by <see cref="InitiateVisitRequestCommand"/> (and by resend/recover).
/// <c>SessionToken</c> is an OPAQUE random challenge token (only its SHA-256 is stored
/// server-side — it is NOT the email). The frontend keeps it and passes it back to
/// verify/resend/recover. Attempt/cooldown metadata is presentation input only — the
/// server independently enforces both.
/// </summary>
public sealed record InitiateVisitRequestResponse(
    string SessionToken,
    string Message,
    string MaskedEmail,
    DateTime ExpiresAt,
    int ResendAfterSeconds,
    int MaxAttempts);
