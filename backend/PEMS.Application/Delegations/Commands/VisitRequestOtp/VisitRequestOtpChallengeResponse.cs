namespace PEMS.Application.Delegations.Commands.VisitRequestOtp;

/// <summary>
/// The OTP challenge handed back by the public visit-request flows: initiate (v2), resend and recover.
///
/// <c>SessionToken</c> is an OPAQUE random challenge token (only its SHA-256 is stored server-side — it is
/// NOT the email). The frontend keeps it and passes it back to verify/resend/recover. Attempt and cooldown
/// metadata is presentation input only; the server independently enforces both.
///
/// Lives in its own Pure V2 namespace because it is shared by three live flows. It previously sat inside the
/// retired V1 <c>InitiateVisitRequest</c> folder, which coupled the surviving OTP flows to dead V1 code.
/// </summary>
public sealed record InitiateVisitRequestResponse(
    string SessionToken,
    string Message,
    string MaskedEmail,
    DateTime ExpiresAt,
    int ResendAfterSeconds,
    int MaxAttempts);
